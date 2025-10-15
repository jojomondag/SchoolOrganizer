using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text.Json;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Drive.v3;
using SchoolOrganizer.Src.Services;
using SchoolOrganizer.Src.Services.Utilities;
using Serilog;

namespace SchoolOrganizer.Src.Services;

public class DownloadManager
{
    private readonly CachedClassroomDataService _classroomService;
    private readonly DriveService _driveService;
    private readonly SemaphoreSlim _semaphore;
    private string _selectedFolderPath;
    private readonly Action<string> _updateStatus;
    private readonly string _teacherName;
    private readonly Dictionary<string, DateTime> _courseDownloadTimes;
    private static readonly string CourseDownloadFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SchoolOrganizer",
        "course_download_times.json");
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
        _courseDownloadTimes = LoadFile<Dictionary<string, DateTime>>(CourseDownloadFilePath) ?? new Dictionary<string, DateTime>();
    }
    public void UpdateDownloadFolder(string newFolderPath) => _selectedFolderPath = newFolderPath;
    public async Task DownloadAssignmentsAsync(Course course)
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

            UpdateStatus($"Downloading assignments for {course.Name}...");
            Log.Information($"Downloading assignments for {course.Name}...");

            Log.Information($"About to call GetCourseDataAsync for course ID: {course.Id}");
            var (students, submissions, courseWorks) = await GetCourseDataAsync(course.Id);
            Log.Information($"GetCourseDataAsync completed for course ID: {course.Id}");
            if (students.Count == 0 || courseWorks.Count == 0)
            {
                UpdateStatus($"No students or assignments found for {course.Name}.");
                Log.Warning($"No data found for course {course.Name}: Students={students.Count}, CourseWorks={courseWorks.Count}");
                return;
            }

            Log.Information($"Found {students.Count} students, {submissions.Count} submissions, {courseWorks.Count} course works");

            string courseDirectory = GetOrCreateCourseDirectory(_selectedFolderPath, course.Name, course.Section, course.Id, _teacherName);
            UpdateStatus($"Created course directory: {courseDirectory}");

            // Process all students, even if they don't have submissions
            await ProcessStudentsAsync(students, submissions, courseWorks, courseDirectory);

            // Use UpdateStatus instead of _updateStatus
            UpdateStatus("Extracting ZIP and RAR files...");
            await FileExtractor.ExtractZipAndRARFilesFromFoldersAsync(courseDirectory);
            UpdateStatus("ZIP and RAR files extracted and removed.");

            UpdateLastDownloadTime(course.Id, DateTime.UtcNow);
            
            UpdateStatus($"Download and processing completed for course {course.Name}.");
            Log.Information($"Successfully completed download for course {course.Name}");
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
        
        List<Student> students = new List<Student>();
        List<StudentSubmission> submissions = new List<StudentSubmission>();
        List<CourseWork> courseWorks = new List<CourseWork>();
        
        // Add overall timeout to prevent hanging indefinitely
        using var overallCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        
        try
        {
            // Create tasks for each API call
            Log.Information("Fetching students...");
            var studentsTask = _classroomService.GetStudentsInCourseAsync(courseId);
            
            Log.Information("Fetching submissions...");
            var submissionsTask = _classroomService.GetStudentSubmissionsAsync(courseId);
            
            Log.Information("Fetching course works...");
            var courseWorksTask = _classroomService.GetCourseWorkAsync(courseId);

            Log.Information("Waiting for all API calls to complete...");
            
            // Wait for all tasks to complete, handling exceptions individually
            try
            {
                await Task.WhenAll(studentsTask, submissionsTask, courseWorksTask).WaitAsync(overallCts.Token);
                
                // If we get here, all tasks completed successfully
                students = studentsTask.Result.ToList();
                submissions = submissionsTask.Result.ToList();
                courseWorks = courseWorksTask.Result;
                
                Log.Information($"API calls completed successfully. Students: {students.Count}, Submissions: {submissions.Count}, CourseWorks: {courseWorks.Count}");
            }
            catch (Exception ex)
            {
                Log.Error($"One or more API calls failed: {ex.Message}");
                
                // Try to get results from completed tasks
                try
                {
                    if (studentsTask.IsCompletedSuccessfully)
                    {
                        students = studentsTask.Result.ToList();
                        Log.Information($"Students task completed successfully: {students.Count} students");
                    }
                    else if (studentsTask.IsFaulted)
                    {
                        Log.Error($"Students task failed: {studentsTask.Exception?.GetBaseException().Message}");
                    }
                }
                catch (Exception studentEx)
                {
                    Log.Error($"Error getting students result: {studentEx.Message}");
                }
                
                try
                {
                    if (submissionsTask.IsCompletedSuccessfully)
                    {
                        submissions = submissionsTask.Result.ToList();
                        Log.Information($"Submissions task completed successfully: {submissions.Count} submissions");
                    }
                    else if (submissionsTask.IsFaulted)
                    {
                        Log.Error($"Submissions task failed: {submissionsTask.Exception?.GetBaseException().Message}");
                    }
                }
                catch (Exception submissionEx)
                {
                    Log.Error($"Error getting submissions result: {submissionEx.Message}");
                }
                
                try
                {
                    if (courseWorksTask.IsCompletedSuccessfully)
                    {
                        courseWorks = courseWorksTask.Result;
                        Log.Information($"Course works task completed successfully: {courseWorks.Count} course works");
                    }
                    else if (courseWorksTask.IsFaulted)
                    {
                        Log.Error($"Course works task failed: {courseWorksTask.Exception?.GetBaseException().Message}");
                    }
                }
                catch (Exception courseWorkEx)
                {
                    Log.Error($"Error getting course works result: {courseWorkEx.Message}");
                }
                
                Log.Information($"Partial results obtained. Students: {students.Count}, Submissions: {submissions.Count}, CourseWorks: {courseWorks.Count}");
            }
        }
        catch (OperationCanceledException)
        {
            Log.Error($"Overall timeout reached for course data fetch for course {courseId}");
            UpdateStatus($"Timeout reached while fetching course data for {courseId}. Using partial results if available.");
        }
        catch (Exception ex)
        {
            Log.Error($"Unexpected error in GetCourseDataAsync for course {courseId}: {ex.Message}");
            UpdateStatus($"Error fetching course data: {ex.Message}");
        }

        return (students, submissions, courseWorks);
    }
    private async Task ProcessStudentsAsync(List<Student> students, List<StudentSubmission> submissions, List<CourseWork> courseWorks, string courseDirectory)
    {
        var studentDict = students.ToDictionary(s => s.UserId, s => s);
        var courseWorkDict = courseWorks.ToDictionary(cw => cw.Id, cw => cw);
        var submissionDict = submissions.GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.ToList());

        var tasks = new List<Task>();

        foreach (var student in students)
        {
            string studentDirectory = DirectoryUtil.CreateStudentDirectory(courseDirectory, student);

            // Process submissions for this student
            if (submissionDict.TryGetValue(student.UserId, out var studentSubmissions))
            {
                tasks.Add(ProcessSubmissionsAsync(studentSubmissions, student, courseWorkDict, studentDirectory));
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
    private async Task ProcessSubmissionsAsync(List<StudentSubmission> submissions, Student student, 
        Dictionary<string, CourseWork> courseWorkDict, string studentDirectory)
    {
        var tasks = new List<Task>();
        var processedAttachments = new HashSet<string>();

        foreach (var submission in submissions)
        {
            if (submission.AssignmentSubmission?.Attachments == null)
                continue;

            var courseWork = courseWorkDict.GetValueOrDefault(submission.CourseWorkId);
            string studentName = student.Profile.Name.FullName;
            string assignmentName = courseWork?.Title ?? "Unknown Assignment";
            string assignmentDirectory = DirectoryUtil.CreateAssignmentDirectory(studentDirectory, new CourseWork { Title = assignmentName });

            foreach (var attachment in submission.AssignmentSubmission.Attachments)
            {
                string attachmentId = attachment.DriveFile?.Id ?? attachment.Link?.Url ?? "";
                if (!string.IsNullOrEmpty(attachmentId) && !processedAttachments.Contains(attachmentId))
                {
                    processedAttachments.Add(attachmentId);
                    tasks.Add(ProcessAttachmentAsync(attachment, assignmentDirectory, submission.Id, studentName, assignmentName));
                }
            }
        }

        await Task.WhenAll(tasks);
    }
    private async Task ProcessAttachmentAsync(Attachment attachment, string assignmentDirectory, string submissionId, string studentName, string assignmentName)
    {
        await _semaphore.WaitAsync();
        try
        {
            // Ensure the assignment directory exists
            if (!Directory.Exists(assignmentDirectory))
            {
                Directory.CreateDirectory(assignmentDirectory);
                Log.Information($"Created assignment directory: {assignmentDirectory}");
            }

            if (attachment.DriveFile != null)
            {
                var file = await _driveService.Files.Get(attachment.DriveFile.Id).ExecuteAsync();
                string filePath = Path.Combine(assignmentDirectory, DirectoryUtil.SanitizeFolderName(attachment.DriveFile.Title ?? "File"));

                // Check if the file already exists
                if (File.Exists(filePath))
                {
                    Log.Information($"File already exists: {filePath}. Skipping download.");
                    return;
                }

                string statusMessage = $"Downloading: {attachment.DriveFile.Title} for {studentName} - {assignmentName}";
                UpdateStatus(statusMessage);
                // Remove this duplicate log
                // Log.Information(statusMessage);

                // Check if the file needs to be unpacked
                if (NeedsUnpacking(file.MimeType))
                {
                    // Create a link for files that need unpacking
                    string linkPath = Path.Combine(assignmentDirectory, DirectoryUtil.SanitizeFolderName(attachment.DriveFile.Title ?? "File") + ".url");
                    await File.WriteAllTextAsync(linkPath, $"[InternetShortcut]\nURL=https://drive.google.com/file/d/{attachment.DriveFile.Id}/view");
                    _downloadedFiles[submissionId] = new DownloadedFileInfo(attachment.DriveFile.Id, attachment.DriveFile.Title ?? "File", linkPath, DateTime.UtcNow, studentName, assignmentName, true);
                }
                else
                {
                    // Download and save the file for direct display
                    await DownloadFileAsync(attachment.DriveFile.Id, attachment.DriveFile.Title ?? "Untitled", file.MimeType, assignmentDirectory, submissionId, studentName, assignmentName);
                }
            }
            else if (attachment.Link != null)
            {
                string statusMessage = $"Saving link: {attachment.Link.Title} for {studentName} - {assignmentName}";
                UpdateStatus(statusMessage);
                // Remove this duplicate log
                // Log.Information(statusMessage);
                string linkPath = Path.Combine(assignmentDirectory, DirectoryUtil.SanitizeFolderName(attachment.Link.Title ?? "Link") + ".url");
                await File.WriteAllTextAsync(linkPath, $"[InternetShortcut]\nURL={attachment.Link.Url}");
                _downloadedFiles[submissionId] = new DownloadedFileInfo(attachment.Link.Url, attachment.Link.Title ?? "Link", linkPath, DateTime.UtcNow, studentName, assignmentName, true);
            }
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error processing attachment for {studentName} - {assignmentName}: {ex.Message}";
            UpdateStatus(errorMessage);
            Log.Error(ex, errorMessage);
        }
        finally
        {
            _semaphore.Release();
        }
    }
    private async Task DownloadFileAsync(string fileId, string fileName, string mimeType, string destinationFolder, string submissionId, string studentName, string assignmentName)
    {
        string fileExtension = Path.GetExtension(fileName);
        string sanitizedFileName = DirectoryUtil.SanitizeFolderName(Path.GetFileNameWithoutExtension(fileName));
        string filePath;

        try
        {
            // Ensure the destination directory exists
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
                Log.Information($"Created destination directory: {destinationFolder}");
            }

            if (mimeType.StartsWith("application/vnd.google-apps"))
            {
                var exportMimeType = GetExportMimeType(mimeType);
                fileExtension = GetFileExtension(exportMimeType);
                filePath = Path.Combine(destinationFolder, $"{sanitizedFileName}{fileExtension}");

                // Check if file already exists
                if (File.Exists(filePath))
                {
                    Log.Information($"File already exists, skipping: {filePath}");
                    return;
                }

                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                await _driveService.Files.Export(fileId, exportMimeType).DownloadAsync(fileStream);
            }
            else
            {
                if (string.IsNullOrEmpty(fileExtension))
                {
                    fileExtension = GetFileExtension(mimeType);
                }
                filePath = Path.Combine(destinationFolder, $"{sanitizedFileName}{fileExtension}");

                // Check if file already exists
                if (File.Exists(filePath))
                {
                    Log.Information($"File already exists, skipping: {filePath}");
                    return;
                }

                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                await _driveService.Files.Get(fileId).DownloadAsync(fileStream);
            }

            // Add the IsLink parameter (false in this case, as we're downloading the file)
            _downloadedFiles[submissionId] = new DownloadedFileInfo(fileId, Path.GetFileName(filePath), filePath, DateTime.UtcNow, studentName, assignmentName, false);
            string successMessage = $"Downloaded: {Path.GetFileName(filePath)} for {studentName} - {assignmentName}";
            UpdateStatus(successMessage);
            // Log.Information(successMessage); // Removed this line
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error downloading file {fileName} for {studentName} - {assignmentName}: {ex.Message}";
            UpdateStatus(errorMessage);
            Log.Error(ex, errorMessage); // This is fine as it's logging exceptions
        }
    }
    private string GetExportMimeType(string mimeType) => mimeType switch
    {
        "application/vnd.google-apps.document" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.google-apps.spreadsheet" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.google-apps.presentation" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/vnd.google-apps.drawing" => "image/png",
        "application/vnd.google-apps.script" => "application/vnd.google-apps.script+json",
        "application/vnd.google-apps.form" => "application/pdf",
        "application/vnd.google-apps.jam" => "application/pdf",
        "application/vnd.google-apps.site" => "text/html",
        "application/vnd.google-apps.folder" => "application/vnd.google-apps.folder",
        _ => "application/pdf",
    };
    private string GetFileExtension(string mimeType) => mimeType switch
    {
        "application/pdf" => ".pdf",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
        "application/zip" => ".zip",
        "application/x-rar-compressed" => ".rar",
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        "text/plain" => ".txt",
        "text/csv" => ".csv",
        "application/vnd.ms-excel" => ".xls",
        "application/vnd.ms-powerpoint" => ".ppt",
        "application/msword" => ".doc",
        "application/vnd.google-apps.script+json" => ".json",
        "text/html" => ".html",
        "application/vnd.google-apps.folder" => "",
        _ => ".bin",
    };
    public DateTime? GetLastDownloadTime(string courseId) => _courseDownloadTimes.TryGetValue(courseId, out var dateTime) ? dateTime : null;
    public void UpdateLastDownloadTime(string courseId, DateTime downloadTime)
    {
        _courseDownloadTimes[courseId] = downloadTime;
        SaveFile(CourseDownloadFilePath, _courseDownloadTimes);
    }
    public void ClearCourseDownloadTimes()
    {
        _courseDownloadTimes.Clear();
        SaveFile(CourseDownloadFilePath, _courseDownloadTimes);
    }
    private T? LoadFile<T>(string filePath) where T : class =>
        File.Exists(filePath) ? JsonSerializer.Deserialize<T>(File.ReadAllText(filePath)) : null;
    private void SaveFile<T>(string filePath, T data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var content = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, content);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error($"Error saving file to {filePath}: {ex.Message}");
        }
    }

    public IEnumerable<DownloadedFileInfo> GetDownloadedFiles() => _downloadedFiles.Values;

    public void ClearDownloadedFiles() => _downloadedFiles.Clear();

    private bool NeedsUnpacking(string mimeType)
    {
        return mimeType == "application/zip" || mimeType == "application/x-rar-compressed";
    }

    private void UpdateStatus(string message)
    {
        string trimmedMessage = message.Replace(Environment.NewLine, " ").Trim();
        _updateStatus(trimmedMessage);
        // Decide whether to keep or remove this line based on where you want logging to occur
        // Log.Information(message);
    }

    private string GetOrCreateCourseDirectory(string baseFolderPath, string courseName, string? section, string courseId, string teacherName)
    {
        try
        {
            // Validate base folder path exists
            if (!Directory.Exists(baseFolderPath))
            {
                Directory.CreateDirectory(baseFolderPath);
                Log.Information($"Created base directory: {baseFolderPath}");
            }

            string courseDirectoryName = DirectoryUtil.GetCourseDirectoryName(courseName, section ?? "No Section", courseId, teacherName);
            string courseDirectoryPath = Path.Combine(baseFolderPath, courseDirectoryName);

            if (!Directory.Exists(courseDirectoryPath))
            {
                Directory.CreateDirectory(courseDirectoryPath);
                Log.Information($"Created course directory: {courseDirectoryPath}");
            }
            else
            {
                Log.Information($"Using existing course directory: {courseDirectoryPath}");
            }

            // Verify directory was created successfully
            if (!Directory.Exists(courseDirectoryPath))
            {
                throw new DirectoryNotFoundException($"Failed to create or access course directory: {courseDirectoryPath}");
            }

            return courseDirectoryPath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error creating course directory for {courseName}");
            throw;
        }
    }
}

public record DownloadedFileInfo(string FileId, string FileName, string LocalPath, DateTime DownloadDateTime, string StudentName, string AssignmentName, bool IsLink);