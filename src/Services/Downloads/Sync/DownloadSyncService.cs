using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SchoolOrganizer.Src.Services.Downloads.Models;
using Serilog;

namespace SchoolOrganizer.Src.Services.Downloads.Sync;

/// <summary>
/// Manages download sync state and tracks file manifests for robust incremental sync
/// </summary>
public class DownloadSyncService
{
    private readonly Dictionary<string, DateTime> _courseDownloadTimes; // Legacy support
    private readonly Dictionary<string, CourseManifest> _courseManifests;
    
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SchoolOrganizer");
    
    private static readonly string CourseDownloadFilePath = Path.Combine(
        AppDataPath,
        "course_download_times.json");
    
    private static readonly string ManifestsDirectory = Path.Combine(
        AppDataPath,
        "DownloadManifests");

    public DownloadSyncService()
    {
        _courseDownloadTimes = LoadFile<Dictionary<string, DateTime>>(CourseDownloadFilePath) ?? new Dictionary<string, DateTime>();
        _courseManifests = LoadAllManifests();
    }

    /// <summary>
    /// Gets the last download time for a course (legacy method, uses manifest if available)
    /// </summary>
    public DateTime? GetLastDownloadTime(string courseId)
    {
        if (_courseManifests.TryGetValue(courseId, out var manifest))
        {
            return manifest.LastSyncTimeUtc;
        }
        return _courseDownloadTimes.TryGetValue(courseId, out var dateTime) ? dateTime : null;
    }

    /// <summary>
    /// Updates the last download time for a course (legacy method)
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
        
        // Also clear manifests
        _courseManifests.Clear();
        ClearAllManifests();
    }

    /// <summary>
    /// Gets or creates a manifest for a course
    /// </summary>
    public CourseManifest GetOrCreateManifest(string courseId)
    {
        if (!_courseManifests.TryGetValue(courseId, out var manifest))
        {
            manifest = new CourseManifest
            {
                CourseId = courseId,
                LastSyncTimeUtc = GetLastDownloadTime(courseId) ?? DateTime.MinValue
            };
            _courseManifests[courseId] = manifest;
        }
        return manifest;
    }

    /// <summary>
    /// Gets the manifest for a course, or null if it doesn't exist
    /// </summary>
    public CourseManifest? GetManifest(string courseId)
    {
        return _courseManifests.TryGetValue(courseId, out var manifest) ? manifest : null;
    }

    /// <summary>
    /// Saves the manifest for a course
    /// </summary>
    public void SaveManifest(CourseManifest manifest)
    {
        try
        {
            Directory.CreateDirectory(ManifestsDirectory);
            string manifestPath = GetManifestPath(manifest.CourseId);
            var content = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(manifestPath, content);
            _courseManifests[manifest.CourseId] = manifest;
            Log.Debug($"Saved manifest for course {manifest.CourseId}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error saving manifest for course {manifest.CourseId}");
        }
    }

    /// <summary>
    /// Gets all file IDs for a submission
    /// </summary>
    public HashSet<string> GetSubmissionFileIds(string courseId, string submissionId)
    {
        var manifest = GetManifest(courseId);
        if (manifest?.SubmissionFiles.TryGetValue(submissionId, out var fileIds) == true)
        {
            return fileIds;
        }
        return new HashSet<string>();
    }

    /// <summary>
    /// Gets a file manifest entry by file ID
    /// </summary>
    public FileManifestEntry? GetFileEntry(string courseId, string fileId)
    {
        var manifest = GetManifest(courseId);
        return manifest?.Files.TryGetValue(fileId, out var entry) == true ? entry : null;
    }

    private Dictionary<string, CourseManifest> LoadAllManifests()
    {
        var manifests = new Dictionary<string, CourseManifest>();
        
        try
        {
            if (!Directory.Exists(ManifestsDirectory))
                return manifests;

            var manifestFiles = Directory.GetFiles(ManifestsDirectory, "*.json");
            foreach (var filePath in manifestFiles)
            {
                try
                {
                    var manifest = LoadFile<CourseManifest>(filePath);
                    if (manifest != null && !string.IsNullOrEmpty(manifest.CourseId))
                    {
                        manifests[manifest.CourseId] = manifest;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, $"Failed to load manifest from {filePath}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading manifests");
        }

        return manifests;
    }

    private void ClearAllManifests()
    {
        try
        {
            if (Directory.Exists(ManifestsDirectory))
            {
                var manifestFiles = Directory.GetFiles(ManifestsDirectory, "*.json");
                foreach (var file in manifestFiles)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error clearing manifests");
        }
    }

    private string GetManifestPath(string courseId)
    {
        // Sanitize course ID for filename
        string sanitized = string.Join("_", courseId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(ManifestsDirectory, $"manifest_{sanitized}.json");
    }

    private T? LoadFile<T>(string filePath) where T : class
    {
        try
        {
            if (!File.Exists(filePath))
                return null;
            
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, $"Error loading file {filePath}");
            return null;
        }
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
            Log.Error(ex, $"Error saving file to {filePath}: {ex.Message}");
        }
    }
}

