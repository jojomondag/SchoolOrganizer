using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SchoolOrganizer.Src.Models.Students;
using SchoolOrganizer.Src.Models.UI;
using SchoolOrganizer.Src.Services.Utilities;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Drive.v3;

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
                    string filePath = Path.Combine(assignmentDirectory, fileName);

                    // Check if the file already exists
                    if (File.Exists(filePath))
                    {
                        return true;
                    }


                    // Download the file
                    using var stream = new MemoryStream();
                    var request = driveService.Files.Get(attachment.DriveFile.Id);
                    await request.DownloadAsync(stream);
                    
                    // Write to file
                    await File.WriteAllBytesAsync(filePath, stream.ToArray());
                    
                    return true;
                }
                else if (attachment.Link != null)
                {
                    // Handle link attachments (URLs)
                    // For now, just log the link - could implement URL download later
                    return false;
                }
                
                return false;
            }
            catch (Exception)
            {
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
            catch (Exception ex)
            {
                return 0;
            }
        }
    }
}
