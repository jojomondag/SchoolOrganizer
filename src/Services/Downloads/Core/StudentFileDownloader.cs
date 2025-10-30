using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Drive.v3;
using SchoolOrganizer.Src.Services;
using SchoolOrganizer.Src.Services.Downloads.Files;
using SchoolOrganizer.Src.Services.Downloads.Utilities;
using Serilog;

namespace SchoolOrganizer.Src.Services.Downloads.Core;

/// <summary>
/// Downloads assignments for a specific student from Google Classroom
/// </summary>
public class StudentFileDownloader
{
    private readonly CachedClassroomDataService _classroomService;
    private readonly DriveService _driveService;
    private readonly SubmissionProcessor _submissionProcessor;

    public StudentFileDownloader(
        CachedClassroomDataService classroomService,
        DriveService driveService)
    {
        _classroomService = classroomService ?? throw new ArgumentNullException(nameof(classroomService));
        _driveService = driveService ?? throw new ArgumentNullException(nameof(driveService));
        
        var fileDownloader = new FileDownloader(driveService);
        var attachmentProcessor = new AttachmentProcessor(driveService, fileDownloader);
        _submissionProcessor = new SubmissionProcessor(attachmentProcessor, semaphore: null); // No semaphore = full parallelization
    }

    /// <summary>
    /// Downloads assignments for a specific student from a specific course
    /// </summary>
    public async Task<bool> DownloadStudentForCourseAsync(
        Course course,
        Student classroomStudent,
        string studentName,
        string downloadFolderPath,
        string teacherName)
    {
        try
        {
            // 1. Get only this student's submissions
            var allSubmissions = await _classroomService.GetStudentSubmissionsAsync(course.Id);
            var studentSubmissions = allSubmissions
                .Where(s => s.UserId == classroomStudent.UserId)
                .ToList();

            if (!studentSubmissions.Any())
            {
                return false;
            }

            // 2. Get course work (assignments)
            var courseWorks = await _classroomService.GetCourseWorkAsync(course.Id);
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

            // 4. Download only this student's files (with full parallelization)
            bool anyFilesDownloaded = await _submissionProcessor.ProcessSubmissionsAsync(
                studentSubmissions,
                classroomStudent,
                courseWorkDict,
                studentDirectory);

            // 5. Extract ZIP and RAR files if any were downloaded
            if (anyFilesDownloaded)
            {
                await Utilities.FileExtractor.ExtractZipAndRARFilesFromFoldersAsync(studentDirectory);
            }

            return anyFilesDownloaded;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error downloading student assignments for {studentName}");
            return false;
        }
    }
}

