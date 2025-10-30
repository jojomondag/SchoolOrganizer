using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Serilog;

namespace SchoolOrganizer.Src.Services.Downloads.Sync;

/// <summary>
/// Manages download sync state and tracks last download times for courses
/// </summary>
public class DownloadSyncService
{
    private readonly Dictionary<string, DateTime> _courseDownloadTimes;
    private static readonly string CourseDownloadFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SchoolOrganizer",
        "course_download_times.json");

    public DownloadSyncService()
    {
        _courseDownloadTimes = LoadFile<Dictionary<string, DateTime>>(CourseDownloadFilePath) ?? new Dictionary<string, DateTime>();
    }

    /// <summary>
    /// Gets the last download time for a course
    /// </summary>
    public DateTime? GetLastDownloadTime(string courseId)
    {
        return _courseDownloadTimes.TryGetValue(courseId, out var dateTime) ? dateTime : null;
    }

    /// <summary>
    /// Updates the last download time for a course
    /// </summary>
    public void UpdateLastDownloadTime(string courseId, DateTime downloadTime)
    {
        _courseDownloadTimes[courseId] = downloadTime;
        SaveFile(CourseDownloadFilePath, _courseDownloadTimes);
    }

    /// <summary>
    /// Clears all course download times
    /// </summary>
    public void ClearCourseDownloadTimes()
    {
        _courseDownloadTimes.Clear();
        SaveFile(CourseDownloadFilePath, _courseDownloadTimes);
    }

    private T? LoadFile<T>(string filePath) where T : class
    {
        return File.Exists(filePath) ? JsonSerializer.Deserialize<T>(File.ReadAllText(filePath)) : null;
    }

    private void SaveFile<T>(string filePath, T data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var content = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, content);
        }
        catch (Exception ex)
        {
            Log.Error($"Error saving file to {filePath}: {ex.Message}");
        }
    }
}

