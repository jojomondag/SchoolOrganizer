using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Drive.v3;
using SchoolOrganizer.Src.Services;
using SchoolOrganizer.Src.Services.Downloads.Files;
using SchoolOrganizer.Src.Services.Downloads.Models;
using SchoolOrganizer.Src.Services.Downloads.Sync;
using SchoolOrganizer.Src.Services.Downloads.Utilities;
using Serilog;

namespace SchoolOrganizer.Src.Services.Downloads.Core;

/// <summary>
/// Manages downloading assignments for entire classrooms with sync support
/// </summary>
public class DownloadManager
{
    private readonly CachedClassroomDataService _classroomService;
    private readonly DriveService _driveService;
    private readonly SemaphoreSlim _semaphore;
    private string _selectedFolderPath;
    private readonly Action<string> _updateStatus;
    private readonly string _teacherName;
    private readonly DownloadSyncService _syncService;
    private readonly AttachmentProcessor _attachmentProcessor;
    private readonly SubmissionProcessor _submissionProcessor;
    private readonly ConcurrentDictionary<string, DownloadedFileInfo> _downloadedFiles;

    public DownloadManager(
        CachedClassroomDataService classroomService,
        DriveService driveService,
        string selectedFolderPath,
        string teacherName,
        Action<string> updateStatus,
        int maxParallelDownloads = 20)
    {
        _classroomService = classroomService ?? throw new ArgumentNullException(nameof(classroomService));
        _driveService = driveService ?? throw new ArgumentNullException(nameof(driveService));
        _selectedFolderPath = selectedFolderPath ?? throw new ArgumentNullException(nameof(selectedFolderPath));
        _teacherName = teacherName ?? throw new ArgumentNullException(nameof(teacherName));
        _updateStatus = updateStatus ?? throw new ArgumentNullException(nameof(updateStatus));
        _semaphore = new SemaphoreSlim(maxParallelDownloads);
        _downloadedFiles = new ConcurrentDictionary<string, DownloadedFileInfo>();
        _syncService = new DownloadSyncService();
        
        var fileDownloader = new Files.FileDownloader(driveService);
        _attachmentProcessor = new AttachmentProcessor(driveService, fileDownloader);
        _submissionProcessor = new SubmissionProcessor(_attachmentProcessor, _semaphore);
    }

    public void UpdateDownloadFolder(string newFolderPath) => _selectedFolderPath = newFolderPath;

    /// <summary>
    /// Downloads assignments for a course with support for incremental sync
    /// </summary>
    /// <param name="course">The course to download</param>
    /// <param name="incrementalSync">If true, only downloads submissions updated since last download</param>
    public async Task DownloadAssignmentsAsync(Course course, bool incrementalSync = false)
    {
        try
        {
            // Validate prerequisites
            if (course == null)
            {
                UpdateStatus("Course is null. Cannot download assignments.");
                Log.Error("Course is null in DownloadAssignmentsAsync");
                return;
            }

            if (string.IsNullOrEmpty(_selectedFolderPath))
            {
                UpdateStatus("Selected folder path is not set. Cannot download assignments.");
                Log.Error("Selected folder path is null or empty");
                return;
            }

            if (_driveService == null)
            {
                UpdateStatus("Drive service is not initialized. Cannot download assignments.");
                Log.Error("Drive service is null");
                return;
            }

            DateTime? lastDownloadTime = incrementalSync ? _syncService.GetLastDownloadTime(course.Id) : null;
            string operation = incrementalSync && lastDownloadTime.HasValue ? "Syncing" : "Downloading";

            UpdateStatus($"{operation} assignments for {course.Name}...");
            Log.Information($"{operation} assignments for {course.Name}... (Incremental: {incrementalSync}, Last Download: {lastDownloadTime})");

            Log.Information($"About to call GetCourseDataAsync for course ID: {course.Id}");
            var (students, submissions, courseWorks) = await GetCourseDataAsync(course.Id);
            Log.Information($"GetCourseDataAsync completed for course ID: {course.Id}");

            // Filter submissions if doing incremental sync
            if (incrementalSync && lastDownloadTime.HasValue)
            {
                var originalCount = submissions.Count;
                submissions = submissions.Where(s =>
                {
                    if (s.UpdateTimeDateTimeOffset == null)
                        return false;

                    var updateTime = s.UpdateTimeDateTimeOffset.Value.DateTime;
                    return updateTime > lastDownloadTime.Value;
                }).ToList();

                Log.Information($"Incremental sync: Filtered {originalCount} submissions to {submissions.Count} updated since {lastDownloadTime.Value}");

                if (submissions.Count == 0)
                {
                    UpdateStatus($"No new submissions for {course.Name} since last sync.");
                    Log.Information($"No updates found for course {course.Name}");
                    return;
                }
            }

            if (students.Count == 0 || courseWorks.Count == 0)
            {
                UpdateStatus($"No students or assignments found for {course.Name}.");
                Log.Warning($"No data found for course {course.Name}: Students={students.Count}, CourseWorks={courseWorks.Count}");
                return;
            }

            Log.Information($"Found {students.Count} students, {submissions.Count} submissions, {courseWorks.Count} course works");

            string courseDirectory = DirectoryUtil.CreateCourseDirectory(
                _selectedFolderPath,
                course.Name ?? "Unknown Course",
                course.Section ?? "No Section",
                course.Id ?? "Unknown ID",
                _teacherName);
            UpdateStatus($"Created course directory: {courseDirectory}");

            // Process all students, even if they don't have submissions
            await ProcessStudentsAsync(students, submissions, courseWorks, courseDirectory);

            // Extract ZIP and RAR files
            UpdateStatus("Extracting ZIP and RAR files...");
            await Utilities.FileExtractor.ExtractZipAndRARFilesFromFoldersAsync(courseDirectory);
            UpdateStatus("ZIP and RAR files extracted and removed.");

            if (!string.IsNullOrEmpty(course.Id))
            {
                _syncService.UpdateLastDownloadTime(course.Id, DateTime.UtcNow);
            }

            UpdateStatus($"{operation} completed for course {course.Name}.");
            Log.Information($"Successfully completed {operation.ToLower()} for course {course.Name}");
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error during download for course {course?.Name ?? "Unknown"}: {ex.Message}";
            UpdateStatus(errorMessage);
            Log.Error(ex, errorMessage);
        }
    }

    private async Task<(List<Student>, List<StudentSubmission>, List<CourseWork>)> GetCourseDataAsync(string courseId)
    {
        Log.Information($"Starting to fetch course data for course ID: {courseId}");

        List<Student> students = new();
        List<StudentSubmission> submissions = new();
        List<CourseWork> courseWorks = new();

        using var overallCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        var studentsTask = _classroomService.GetStudentsInCourseAsync(courseId);
        var submissionsTask = _classroomService.GetStudentSubmissionsAsync(courseId);
        var courseWorksTask = _classroomService.GetCourseWorkAsync(courseId);

        try
        {
            await Task.WhenAll(studentsTask, submissionsTask, courseWorksTask).WaitAsync(overallCts.Token);

            students = studentsTask.Result.ToList();
            submissions = submissionsTask.Result.ToList();
            courseWorks = courseWorksTask.Result;

            Log.Information($"API calls completed. Students: {students.Count}, Submissions: {submissions.Count}, CourseWorks: {courseWorks.Count}");
        }
        catch (OperationCanceledException)
        {
            Log.Error($"Timeout reached for course data fetch for course {courseId}");
            UpdateStatus($"Timeout reached while fetching course data. Using partial results if available.");
            TryGetPartialResults(studentsTask, submissionsTask, courseWorksTask, ref students, ref submissions, ref courseWorks);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error in GetCourseDataAsync for course {courseId}");
            UpdateStatus($"Error fetching course data: {ex.Message}");
            TryGetPartialResults(studentsTask, submissionsTask, courseWorksTask, ref students, ref submissions, ref courseWorks);
        }

        return (students, submissions, courseWorks);
    }

    private static void TryGetPartialResults(
        Task<IList<Student>> studentsTask,
        Task<List<StudentSubmission>> submissionsTask,
        Task<List<CourseWork>> courseWorksTask,
        ref List<Student> students,
        ref List<StudentSubmission> submissions,
        ref List<CourseWork> courseWorks)
    {
        if (studentsTask.IsCompletedSuccessfully)
            students = studentsTask.Result.ToList();
        else if (studentsTask.IsFaulted)
            Log.Error($"Students task failed: {studentsTask.Exception?.GetBaseException().Message}");

        if (submissionsTask.IsCompletedSuccessfully)
            submissions = submissionsTask.Result.ToList();
        else if (submissionsTask.IsFaulted)
            Log.Error($"Submissions task failed: {submissionsTask.Exception?.GetBaseException().Message}");

        if (courseWorksTask.IsCompletedSuccessfully)
            courseWorks = courseWorksTask.Result;
        else if (courseWorksTask.IsFaulted)
            Log.Error($"Course works task failed: {courseWorksTask.Exception?.GetBaseException().Message}");

        Log.Information($"Partial results: Students: {students.Count}, Submissions: {submissions.Count}, CourseWorks: {courseWorks.Count}");
    }

    private async Task ProcessStudentsAsync(List<Student> students, List<StudentSubmission> submissions, List<CourseWork> courseWorks, string courseDirectory)
    {
        var courseWorkDict = courseWorks.ToDictionary(cw => cw.Id, cw => cw);
        var submissionDict = submissions.GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.ToList());

        var tasks = new List<Task>();

        foreach (var student in students)
        {
            string studentDirectory = DirectoryUtil.CreateStudentDirectory(courseDirectory, student);

            // Process submissions for this student
            if (submissionDict.TryGetValue(student.UserId, out var studentSubmissions))
            {
                tasks.Add(_submissionProcessor.ProcessSubmissionsAsync(
                    studentSubmissions,
                    student,
                    courseWorkDict,
                    studentDirectory,
                    UpdateStatus));
            }
            else
            {
                // Create an empty folder for students without submissions
                Log.Information($"No submissions found for student: {student.Profile.Name.FullName}");
                Directory.CreateDirectory(studentDirectory);
            }
        }

        await Task.WhenAll(tasks);
    }

    public DateTime? GetLastDownloadTime(string courseId) => _syncService.GetLastDownloadTime(courseId);

    public void UpdateLastDownloadTime(string courseId, DateTime downloadTime) => _syncService.UpdateLastDownloadTime(courseId, downloadTime);

    public void ClearCourseDownloadTimes() => _syncService.ClearCourseDownloadTimes();

    public IEnumerable<DownloadedFileInfo> GetDownloadedFiles() => _downloadedFiles.Values;

    public void ClearDownloadedFiles() => _downloadedFiles.Clear();

    private void UpdateStatus(string message)
    {
        string trimmedMessage = message.Replace(Environment.NewLine, " ").Trim();
        _updateStatus(trimmedMessage);
    }

}

