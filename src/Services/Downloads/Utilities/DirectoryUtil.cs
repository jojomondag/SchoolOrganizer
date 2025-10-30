using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Google.Apis.Classroom.v1.Data;
using Serilog;

namespace SchoolOrganizer.Src.Services.Downloads.Utilities;

/// <summary>
/// Directory utilities specific to download operations
/// </summary>
public static class DirectoryUtil
{
    public static string SanitizeFolderName(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return "Unnamed";

        // Remove invalid characters
        string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        string sanitized = new string(folderName.Where(ch => !invalidChars.Contains(ch)).ToArray());

        // Replace spaces and colons with underscores
        sanitized = sanitized.Replace(' ', '_').Replace(':', '_');

        // Remove leading and trailing periods and spaces
        sanitized = sanitized.Trim('.', ' ');

        // Ensure the name is not empty after sanitization
        if (string.IsNullOrEmpty(sanitized))
            return "Unnamed";

        // Truncate if the name is too long (adjust the max length as needed)
        const int maxLength = 255;
        if (sanitized.Length > maxLength)
            sanitized = sanitized.Substring(0, maxLength);

        return sanitized;
    }

    public static string GetCourseDirectoryName(string courseName, string className, string courseId, string teacherName)
    {
        // Remove year from courseName and className if present
        string sanitizedCourseName = SanitizeFolderName(RemoveYear(courseName));
        string sanitizedClassName = SanitizeFolderName(RemoveYear(className));
        string sanitizedCourseId = SanitizeFolderName(courseId);
        string sanitizedTeacherName = SanitizeFolderName(teacherName);

        // Combine the parts and remove any duplicate underscores
        string combinedName = $"{sanitizedCourseName}_{sanitizedClassName}_{sanitizedCourseId}_{sanitizedTeacherName}";
        combinedName = Regex.Replace(combinedName, @"_{2,}", "_");

        return combinedName;
    }

    // Helper method to remove year from a string
    private static string RemoveYear(string input)
    {
        return Regex.Replace(input, @"\b\d{4}\b", "").Trim();
    }

    public static string CreateCourseDirectory(string baseFolderPath, string courseName, string className, string courseId, string teacherName)
    {
        // Directory.CreateDirectory handles existence automatically (no-op if exists)
        Directory.CreateDirectory(baseFolderPath);

        string courseDirectoryName = GetCourseDirectoryName(courseName, className, courseId, teacherName);
        string courseDirectoryPath = Path.Combine(baseFolderPath, courseDirectoryName);
        
        Directory.CreateDirectory(courseDirectoryPath);
        Log.Debug($"Ensured course directory exists: {courseDirectoryPath}");

        return courseDirectoryPath;
    }

    public static string CreateStudentDirectory(string baseFolderPath, Student student)
    {
        try
        {
            Directory.CreateDirectory(baseFolderPath);

            string sanitizedStudentName = SanitizeFolderName(student.Profile.Name.FullName);
            string studentDirectoryPath = Path.Combine(baseFolderPath, sanitizedStudentName);
            
            Directory.CreateDirectory(studentDirectoryPath);
            Log.Debug($"Ensured student directory exists: {studentDirectoryPath}");

            return studentDirectoryPath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error creating student directory for {student.Profile.Name.FullName}");
            throw;
        }
    }

    public static string CreateAssignmentDirectory(string studentDirectoryPath, CourseWork courseWork)
    {
        try
        {
            Directory.CreateDirectory(studentDirectoryPath);

            string sanitizedAssignmentName = SanitizeFolderName(courseWork.Title);
            string assignmentDirectoryPath = Path.Combine(studentDirectoryPath, sanitizedAssignmentName);
            
            Directory.CreateDirectory(assignmentDirectoryPath);
            Log.Debug($"Ensured assignment directory exists: {assignmentDirectoryPath}");

            return assignmentDirectoryPath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error creating assignment directory for {courseWork.Title}");
            throw;
        }
    }
}

