using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using SchoolOrganizer.Src.Services.Utilities;
using SchoolOrganizer.Src.Models.Assignments;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Drive.v3;
using Serilog;

namespace SchoolOrganizer.Src.Services
{
    /// <summary>
    /// Service for managing student assignment operations including folder discovery and downloading.
    /// Extracted from StudentGalleryView to separate business logic from UI concerns.
    /// </summary>
    public class StudentAssignmentService
    {
        private readonly SettingsService _settingsService;

        public StudentAssignmentService()
        {
            _settingsService = SettingsService.Instance;
        }

        /// <summary>
        /// Finds the assignment folder for a specific student by searching through course folders
        /// </summary>
        /// <param name="student">The student to find assignments for</param>
        /// <returns>Path to the student's assignment folder, or null if not found</returns>
        public Task<string?> FindStudentAssignmentFolderAsync(SchoolOrganizer.Src.Models.Students.Student student)
        {
            try
            {
                var downloadFolderPath = _settingsService.LoadDownloadFolderPath();
                if (string.IsNullOrEmpty(downloadFolderPath) || !Directory.Exists(downloadFolderPath))
                {
                    return Task.FromResult<string?>(null);
                }

                var sanitizedStudentName = DirectoryUtil.SanitizeFolderName(student.Name);

                // Search through all course folders
                var courseFolders = Directory.GetDirectories(downloadFolderPath);
                foreach (var courseFolder in courseFolders)
                {
                    var studentFolderPath = Path.Combine(courseFolder, sanitizedStudentName);
                    if (Directory.Exists(studentFolderPath))
                    {
                        var fileCount = Directory.GetFiles(studentFolderPath, "*", SearchOption.AllDirectories).Length;
                        if (fileCount > 0)
                        {
                            return Task.FromResult<string?>(studentFolderPath);
                        }
                    }
                }

                return Task.FromResult<string?>(null);
            }
            catch (Exception)
            {
                return Task.FromResult<string?>(null);
            }
        }

        /// <summary>
        /// Downloads assignments for a specific student from Google Classroom
        /// </summary>
        /// <param name="student">The student to download assignments for</param>
        /// <param name="authService">Google authentication service</param>
        /// <param name="teacherName">Name of the teacher</param>
        /// <returns>True if download was successful, false otherwise</returns>
        public async Task<bool> DownloadStudentAssignmentsAsync(SchoolOrganizer.Src.Models.Students.Student student, GoogleAuthService authService, string teacherName)
        {
            try
            {

                // Check if we have Google authentication
                if (authService == null || !await authService.CheckAndAuthenticateAsync())
                {
                    return false;
                }

                // Get download folder path
                var downloadFolderPath = _settingsService.LoadDownloadFolderPath();
                if (string.IsNullOrEmpty(downloadFolderPath) || !Directory.Exists(downloadFolderPath))
                {
                    return false;
                }

                // Get all active courses
                var classroomService = authService.ClassroomService;
                if (classroomService == null)
                {
                    return false;
                }

                var classroomDataService = new ClassroomDataService(classroomService);
                var cachedClassroomService = new CachedClassroomDataService(classroomDataService);
                var courses = await cachedClassroomService.GetActiveClassroomsAsync();


                // Find the course that matches the student's class name
                var matchingCourse = courses.FirstOrDefault(c => 
                    c.Name?.Equals(student.ClassName, StringComparison.OrdinalIgnoreCase) == true);

                if (matchingCourse == null)
                {
                    return false;
                }


                try
                {
                    // Get students in this course
                    var studentsInCourse = await classroomDataService.GetStudentsInCourseAsync(matchingCourse.Id);
                    
                    // Check if our student is enrolled in this course (match by email)
                    var matchingStudent = studentsInCourse.FirstOrDefault(s => 
                        string.Equals(s.Profile?.EmailAddress, student.Email, StringComparison.OrdinalIgnoreCase));

                    if (matchingStudent == null)
                    {
                        return false;
                    }

                    
                    // Download assignments for this student from this course
                    var success = await DownloadStudentForCourseAsync(
                        cachedClassroomService,
                        authService.DriveService!,
                        matchingCourse,
                        matchingStudent,
                        student.Name,
                        downloadFolderPath,
                        teacherName);
                    
                    return success;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Downloads a specific student's assignments from a specific course
        /// </summary>
        private async Task<bool> DownloadStudentForCourseAsync(
            CachedClassroomDataService classroomService,
            DriveService driveService,
            Course course,
            Google.Apis.Classroom.v1.Data.Student classroomStudent,
            string studentName,
            string downloadFolderPath,
            string teacherName)
        {
            try
            {
                
                // 1. Get only this student's submissions
                var allSubmissions = await classroomService.GetStudentSubmissionsAsync(course.Id);
                var studentSubmissions = allSubmissions
                    .Where(s => s.UserId == classroomStudent.UserId)
                    .ToList();
                
                if (!studentSubmissions.Any())
                {
                    return false;
                }

                // 2. Get course work (assignments)
                var courseWorks = await classroomService.GetCourseWorkAsync(course.Id);
                var courseWorkDict = courseWorks.ToDictionary(cw => cw.Id ?? "", cw => cw);

                // 3. Create directories
                var courseDirectory = DirectoryUtil.CreateCourseDirectory(
                    downloadFolderPath,
                    course.Name ?? "Unknown Course",
                    course.Section ?? "No Section",
                    course.Id ?? "Unknown ID",
                    teacherName
                );

                var studentDirectory = DirectoryUtil.CreateStudentDirectory(courseDirectory, classroomStudent);

                // 4. Download only this student's files
                var processedAttachments = new HashSet<string>();
                bool anyFilesDownloaded = false;

                foreach (var submission in studentSubmissions)
                {
                    if (submission.AssignmentSubmission?.Attachments == null)
                        continue;

                    var courseWork = courseWorkDict.GetValueOrDefault(submission.CourseWorkId);
                    string assignmentName = courseWork?.Title ?? "Unknown Assignment";
                    string assignmentDirectory = DirectoryUtil.CreateAssignmentDirectory(studentDirectory, new Google.Apis.Classroom.v1.Data.CourseWork { Title = assignmentName });

                    foreach (var attachment in submission.AssignmentSubmission.Attachments)
                    {
                        string attachmentId = attachment.DriveFile?.Id ?? attachment.Link?.Url ?? "";
                        if (!string.IsNullOrEmpty(attachmentId) && !processedAttachments.Contains(attachmentId))
                        {
                            processedAttachments.Add(attachmentId);
                            var success = await DownloadStudentAttachmentAsync(
                                driveService,
                                attachment,
                                assignmentDirectory,
                                submission.Id,
                                studentName,
                                assignmentName);
                            if (success)
                            {
                                anyFilesDownloaded = true;
                            }
                        }
                    }
                }

                // 5. Extract ZIP and RAR files if any were downloaded
                if (anyFilesDownloaded)
                {
                    await FileExtractor.ExtractZipAndRARFilesFromFoldersAsync(studentDirectory);
                }

                return anyFilesDownloaded;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Downloads a single attachment for a student
        /// </summary>
        private async Task<bool> DownloadStudentAttachmentAsync(
            DriveService driveService,
            Google.Apis.Classroom.v1.Data.Attachment attachment,
            string assignmentDirectory,
            string submissionId,
            string studentName,
            string assignmentName)
        {
            try
            {
                if (attachment.DriveFile != null)
                {
                    var file = await driveService.Files.Get(attachment.DriveFile.Id).ExecuteAsync();
                    string fileName = DirectoryUtil.SanitizeFolderName(attachment.DriveFile.Title ?? "File");
                    string mimeType = file.MimeType ?? "";
                    string filePath;

                    // Check if the file needs to be unpacked (ZIP/RAR) - create URL shortcut instead of downloading
                    if (NeedsUnpacking(mimeType))
                    {
                        // Create a link for files that need unpacking
                        string linkPath = Path.Combine(assignmentDirectory, fileName + ".url");
                        linkPath = DirectoryUtil.GetUniqueFilePath(linkPath);
                        await File.WriteAllTextAsync(linkPath, $"[InternetShortcut]\nURL=https://drive.google.com/file/d/{attachment.DriveFile.Id}/view");
                        Log.Information("Created URL shortcut for archive: {LinkPath}", linkPath);
                        return true;
                    }
                    // Check if this is a Google Docs file
                    else if (mimeType.StartsWith("application/vnd.google-apps"))
                    {
                        // Handle Google Docs - export to appropriate format
                        var exportMimeType = GetExportMimeType(mimeType);
                        var fileExtension = GetFileExtension(exportMimeType);
                        string sanitizedFileName = DirectoryUtil.SanitizeFolderName(Path.GetFileNameWithoutExtension(fileName));
                        filePath = Path.Combine(assignmentDirectory, $"{sanitizedFileName}{fileExtension}");

                        // Get unique file path to handle duplicates
                        filePath = DirectoryUtil.GetUniqueFilePath(filePath);
                        string metadataPath = filePath + ".gdocmeta.json";

                        // Download the file
                        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                        await driveService.Files.Export(attachment.DriveFile.Id, exportMimeType).DownloadAsync(fileStream);
                        Log.Information("Downloaded Google Doc: {FilePath}", filePath);

                        // Save Google Docs metadata
                        await SaveGoogleDocMetadataAsync(attachment.DriveFile.Id, fileName, mimeType, filePath);
                    }
                    else
                    {
                        // Handle regular files
                        filePath = Path.Combine(assignmentDirectory, fileName);

                        // Get unique file path to handle duplicates
                        filePath = DirectoryUtil.GetUniqueFilePath(filePath);

                        // Download the file
                        using var stream = new MemoryStream();
                        var request = driveService.Files.Get(attachment.DriveFile.Id);
                        await request.DownloadAsync(stream);

                        // Write to file
                        await File.WriteAllBytesAsync(filePath, stream.ToArray());
                        Log.Information("Downloaded regular file: {FilePath}", filePath);
                    }

                    return true;
                }
                else if (attachment.Link != null)
                {
                    // Handle link attachments by creating .url files
                    string linkPath = Path.Combine(assignmentDirectory, DirectoryUtil.SanitizeFolderName(attachment.Link.Title ?? "Link") + ".url");
                    linkPath = DirectoryUtil.GetUniqueFilePath(linkPath);
                    await File.WriteAllTextAsync(linkPath, $"[InternetShortcut]\nURL={attachment.Link.Url}");
                    Log.Information("Saved link attachment: {LinkPath}", linkPath);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error downloading attachment for {StudentName} - {AssignmentName}", studentName, assignmentName);
                return false;
            }
        }

        /// <summary>
        /// Gets the count of files in a student's assignment folder
        /// </summary>
        /// <param name="folderPath">Path to the student's assignment folder</param>
        /// <returns>Number of files in the folder</returns>
        public int GetAssignmentFileCount(string folderPath)
        {
            try
            {
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                    return 0;

                return Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories).Length;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets the export MIME type for Google Docs files
        /// </summary>
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

        /// <summary>
        /// Gets the file extension for a given MIME type
        /// </summary>
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

        /// <summary>
        /// Checks if a file needs unpacking (ZIP or RAR)
        /// </summary>
        private bool NeedsUnpacking(string mimeType)
        {
            return mimeType == "application/zip" || mimeType == "application/x-rar-compressed";
        }

        /// <summary>
        /// Saves Google Docs metadata as a JSON file alongside the downloaded file
        /// </summary>
        private async Task SaveGoogleDocMetadataAsync(string fileId, string fileName, string mimeType, string downloadedFilePath)
        {
            try
            {
                var metadata = new GoogleDocMetadata
                {
                    FileId = fileId,
                    OriginalTitle = fileName,
                    MimeType = mimeType,
                    DocType = GoogleDocMetadata.GetDocTypeFromMimeType(mimeType),
                    WebViewLink = $"https://drive.google.com/file/d/{fileId}/view",
                    DownloadedFilePath = downloadedFilePath,
                    CreatedAt = DateTime.UtcNow
                };

                // Save metadata file with .gdocmeta.json extension
                string metadataPath = downloadedFilePath + ".gdocmeta.json";
                var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(metadataPath, json);

                Log.Information("Saved Google Docs metadata: {MetadataPath}", metadataPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving Google Docs metadata for {FileName}", fileName);
            }
        }
    }
}
