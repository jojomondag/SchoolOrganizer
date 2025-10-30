using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SchoolOrganizer.Src.Services.Downloads.Core;
using SchoolOrganizer.Src.Services.Utilities;
using Google.Apis.Classroom.v1.Data;
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
                    var downloader = new StudentFileDownloader(
                        cachedClassroomService,
                        authService.DriveService!);
                    
                    var success = await downloader.DownloadStudentForCourseAsync(
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

    }
}
