using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

            // Get or create manifest for this course
            var manifest = _syncService.GetOrCreateManifest(course.Id ?? "");
            DateTime? lastDownloadTime = incrementalSync ? manifest.LastSyncTimeUtc : (DateTime?)null;
            string operation = incrementalSync && lastDownloadTime.HasValue && lastDownloadTime.Value != DateTime.MinValue ? "Syncing" : "Downloading";

            UpdateStatus($"{operation} assignments for {course.Name}...");
            Log.Information($"{operation} assignments for {course.Name}... (Incremental: {incrementalSync}, Last Download: {lastDownloadTime})");

            string courseId = course.Id ?? "";
            Log.Information($"About to call GetCourseDataAsync for course ID: {courseId}");
            var (students, submissions, courseWorks) = await GetCourseDataAsync(courseId);
            Log.Information($"GetCourseDataAsync completed for course ID: {courseId}");

            // Track current submission IDs for orphan detection
            var currentSubmissionIds = new HashSet<string>(submissions.Select(s => s.Id ?? "").Where(id => !string.IsNullOrEmpty(id)));

            // Filter submissions if doing incremental sync (using UTC for timezone safety)
            if (incrementalSync && lastDownloadTime.HasValue && lastDownloadTime.Value != DateTime.MinValue)
            {
                var originalCount = submissions.Count;
                submissions = submissions.Where(s =>
                {
                    if (s.UpdateTimeDateTimeOffset == null)
                        return false;

                    // Use UTC for consistent comparison
                    var updateTimeUtc = s.UpdateTimeDateTimeOffset.Value.UtcDateTime;
                    return updateTimeUtc > lastDownloadTime.Value;
                }).ToList();

                Log.Information($"Incremental sync: Filtered {originalCount} submissions to {submissions.Count} updated since {lastDownloadTime.Value:yyyy-MM-dd HH:mm:ss} UTC");

                if (submissions.Count == 0)
                {
                    UpdateStatus($"No new submissions for {course.Name} since last sync.");
                    Log.Information($"No updates found for course {course.Name}");
                    
                    // Create course directory first
                    string courseDir = DirectoryUtil.CreateCourseDirectory(
                        _selectedFolderPath,
                        course.Name ?? "Unknown Course",
                        course.Section ?? "No Section",
                        course.Id ?? "Unknown ID",
                        _teacherName);
                    
                    // Still check for orphaned files even if no new submissions
                    await CleanupOrphanedFilesAsync(manifest, currentSubmissionIds, courseDir);
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
            await ProcessStudentsAsync(students, submissions, courseWorks, courseDirectory, manifest);

            // Update manifest with downloaded files
            await UpdateManifestAfterDownloadAsync(manifest, submissions, courseDirectory);

            // Clean up orphaned files (files that were removed from submissions)
            await CleanupOrphanedFilesAsync(manifest, currentSubmissionIds, courseDirectory);

            // Extract ZIP and RAR files
            UpdateStatus("Extracting ZIP and RAR files...");
            await Utilities.FileExtractor.ExtractZipAndRARFilesFromFoldersAsync(courseDirectory);
            UpdateStatus("ZIP and RAR files extracted and removed.");

            // Update sync time (use UTC consistently)
            if (!string.IsNullOrEmpty(course.Id))
            {
                manifest.LastSyncTimeUtc = DateTime.UtcNow;
                _syncService.SaveManifest(manifest);
                _syncService.UpdateLastDownloadTime(course.Id, DateTime.UtcNow); // Legacy support
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
            (students, submissions, courseWorks) = TryGetPartialResults(studentsTask, submissionsTask, courseWorksTask);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error in GetCourseDataAsync for course {courseId}");
            UpdateStatus($"Error fetching course data: {ex.Message}");
            (students, submissions, courseWorks) = TryGetPartialResults(studentsTask, submissionsTask, courseWorksTask);
        }

        return (students, submissions, courseWorks);
    }

    private static (List<Student>, List<StudentSubmission>, List<CourseWork>) TryGetPartialResults(
        Task<IList<Student>> studentsTask,
        Task<List<StudentSubmission>> submissionsTask,
        Task<List<CourseWork>> courseWorksTask)
    {
        var students = studentsTask.IsCompletedSuccessfully ? studentsTask.Result.ToList() : new List<Student>();
        var submissions = submissionsTask.IsCompletedSuccessfully ? submissionsTask.Result.ToList() : new List<StudentSubmission>();
        var courseWorks = courseWorksTask.IsCompletedSuccessfully ? courseWorksTask.Result : new List<CourseWork>();

        if (studentsTask.IsFaulted)
            Log.Error($"Students task failed: {studentsTask.Exception?.GetBaseException().Message}");
        if (submissionsTask.IsFaulted)
            Log.Error($"Submissions task failed: {submissionsTask.Exception?.GetBaseException().Message}");
        if (courseWorksTask.IsFaulted)
            Log.Error($"Course works task failed: {courseWorksTask.Exception?.GetBaseException().Message}");

        Log.Information($"Partial results: Students: {students.Count}, Submissions: {submissions.Count}, CourseWorks: {courseWorks.Count}");
        return (students, submissions, courseWorks);
    }

    private async Task ProcessStudentsAsync(
        List<Student> students, 
        List<StudentSubmission> submissions, 
        List<CourseWork> courseWorks, 
        string courseDirectory,
        CourseManifest manifest)
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
                    UpdateStatus,
                    manifest));
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

    /// <summary>
    /// Updates the manifest with information about downloaded files
    /// </summary>
    private async Task UpdateManifestAfterDownloadAsync(
        CourseManifest manifest,
        List<StudentSubmission> submissions,
        string courseDirectory)
    {
        try
        {
            // We need to scan the course directory to find downloaded files and match them with submissions
            // This is done after download to capture all files including those from FileNamingStrategy
            foreach (var submission in submissions)
            {
                if (string.IsNullOrEmpty(submission.Id) || submission.AssignmentSubmission?.Attachments == null)
                    continue;

                var submissionFileIds = new HashSet<string>();

                foreach (var attachment in submission.AssignmentSubmission.Attachments)
                {
                    string? fileId = attachment.DriveFile?.Id;
                    if (string.IsNullOrEmpty(fileId))
                        continue;

                    submissionFileIds.Add(fileId);

                    // Try to find the local file path
                    // Files are organized as: courseDirectory/studentName/assignmentName/fileName
                    string? localPath = await FindLocalFilePathAsync(courseDirectory, fileId, attachment.DriveFile?.Title);

                    if (localPath != null)
                    {
                        // Get Drive file metadata for modification time
                        DateTime? driveModifiedTime = null;
                        try
                        {
                            var driveFile = await _driveService.Files.Get(fileId).ExecuteAsync();
                            driveModifiedTime = driveFile.ModifiedTimeDateTimeOffset?.UtcDateTime;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"Failed to get Drive metadata for file {fileId}");
                        }

                        // Update or create manifest entry
                        var entry = new FileManifestEntry
                        {
                            FileId = fileId,
                            SubmissionId = submission.Id,
                            StudentUserId = submission.UserId ?? "",
                            CourseWorkId = submission.CourseWorkId ?? "",
                            LocalPath = localPath,
                            DriveModifiedTimeUtc = driveModifiedTime,
                            LocalModifiedTimeUtc = File.Exists(localPath) ? File.GetLastWriteTimeUtc(localPath) : DateTime.UtcNow,
                            IsLink = localPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase),
                            FileName = Path.GetFileName(localPath),
                            LastSyncedUtc = DateTime.UtcNow
                        };

                        manifest.Files[fileId] = entry;
                    }
                }

                // Update submission files mapping
                manifest.SubmissionFiles[submission.Id] = submissionFileIds;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating manifest after download");
        }
    }

    /// <summary>
    /// Finds the local file path for a given Drive file ID by scanning the course directory
    /// </summary>
    private async Task<string?> FindLocalFilePathAsync(string courseDirectory, string fileId, string? fileName)
    {
        try
        {
            if (!Directory.Exists(courseDirectory))
                return null;

            // Check Google Docs metadata files first (they contain file IDs)
            var metadataFiles = Directory.GetFiles(courseDirectory, "*.gdocmeta.json", SearchOption.AllDirectories);
            foreach (var metadataPath in metadataFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(metadataPath);
                    var metadata = System.Text.Json.JsonSerializer.Deserialize<SchoolOrganizer.Src.Models.Assignments.GoogleDocMetadata>(json);
                    if (metadata?.FileId == fileId)
                    {
                        // Return the corresponding file (without .gdocmeta.json extension)
                        string filePath = metadataPath.Substring(0, metadataPath.Length - ".gdocmeta.json".Length);
                        if (File.Exists(filePath))
                            return filePath;
                    }
                }
                catch
                {
                    // Continue searching
                }
            }

            // For regular files, we'd need to check file contents or use a different strategy
            // For now, we'll rely on the FileNamingStrategy which should produce predictable paths
            // This is a limitation - we may need to improve this by tracking paths during download
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, $"Error finding local file path for {fileId}");
            return null;
        }
    }

    /// <summary>
    /// Cleans up orphaned files that are no longer in any submission
    /// </summary>
    private Task CleanupOrphanedFilesAsync(
        CourseManifest manifest,
        HashSet<string> currentSubmissionIds,
        string courseDirectory)
    {
        try
        {
            if (!Directory.Exists(courseDirectory))
                return Task.CompletedTask;

            var filesToDelete = new List<string>();
            var submissionIdsToRemove = new List<string>();

            // Find submissions that no longer exist
            foreach (var kvp in manifest.SubmissionFiles)
            {
                if (!currentSubmissionIds.Contains(kvp.Key))
                {
                    // This submission no longer exists - mark its files for deletion
                    submissionIdsToRemove.Add(kvp.Key);
                    foreach (var fileId in kvp.Value)
                    {
                        if (manifest.Files.TryGetValue(fileId, out var entry))
                        {
                            // Check if this file is still referenced by other submissions
                            bool stillReferenced = manifest.SubmissionFiles.Any(sf => 
                                sf.Key != kvp.Key && sf.Value.Contains(fileId));

                            if (!stillReferenced && File.Exists(entry.LocalPath))
                            {
                                filesToDelete.Add(entry.LocalPath);
                                
                                // Also delete metadata file if it exists
                                if (entry.LocalPath.EndsWith(".gdocmeta.json", StringComparison.OrdinalIgnoreCase) == false)
                                {
                                    string metadataPath = entry.LocalPath + ".gdocmeta.json";
                                    if (File.Exists(metadataPath))
                                        filesToDelete.Add(metadataPath);
                                }
                            }

                            // Remove from manifest
                            manifest.Files.Remove(fileId);
                        }
                    }
                }
            }

            // Remove orphaned submission entries
            foreach (var submissionId in submissionIdsToRemove)
            {
                manifest.SubmissionFiles.Remove(submissionId);
            }

            // Delete orphaned files
            int deletedCount = 0;
            foreach (var filePath in filesToDelete)
            {
                try
                {
                    File.Delete(filePath);
                    deletedCount++;
                    Log.Information($"Deleted orphaned file: {filePath}");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, $"Failed to delete orphaned file: {filePath}");
                }
            }

            if (deletedCount > 0)
            {
                UpdateStatus($"Cleaned up {deletedCount} orphaned file(s).");
                Log.Information($"Cleaned up {deletedCount} orphaned files for course");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error cleaning up orphaned files");
        }
        
        return Task.CompletedTask;
    }

    public DateTime? GetLastDownloadTime(string courseId) => _syncService.GetLastDownloadTime(courseId);

    public void UpdateLastDownloadTime(string courseId, DateTime downloadTime) => _syncService.UpdateLastDownloadTime(courseId, downloadTime);

    public void ClearCourseDownloadTimes() => _syncService.ClearCourseDownloadTimes();

    private void UpdateStatus(string message)
    {
        _updateStatus(message.Trim());
    }

}

