using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;

namespace SchoolOrganizer.Services;

/// <summary>
/// Service for downloading and managing profile images from Google Classroom
/// </summary>
public class ImageDownloadService
{
    private readonly HttpClient _httpClient;
    private static readonly string ProfileImagesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "ProfileImages");

    public ImageDownloadService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        // Ensure the profile images directory exists
        if (!Directory.Exists(ProfileImagesDirectory))
        {
            Directory.CreateDirectory(ProfileImagesDirectory);
        }
    }

    /// <summary>
    /// Downloads a profile image from Google Classroom and saves it locally
    /// </summary>
    /// <param name="imageUrl">The URL of the image to download</param>
    /// <param name="studentName">Student name for generating a unique filename</param>
    /// <returns>Local file path if successful, empty string if failed</returns>
    public async Task<string> DownloadProfileImageAsync(string imageUrl, string studentName)
    {
        if (string.IsNullOrEmpty(imageUrl) || string.IsNullOrEmpty(studentName))
        {
            return string.Empty;
        }

        try
        {
            // Clean the student name for filename
            var cleanName = CleanFileName(studentName);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var extension = GetFileExtensionFromUrl(imageUrl);
            var fileName = $"{cleanName}_{timestamp}{extension}";
            var localPath = Path.Combine(ProfileImagesDirectory, fileName);

            // Check if file already exists
            if (File.Exists(localPath))
            {
                Log.Information($"Profile image already exists: {localPath}");
                return localPath;
            }

            Log.Information($"Downloading profile image from: {imageUrl}");
            var response = await _httpClient.GetAsync(imageUrl);
            response.EnsureSuccessStatusCode();

            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(localPath, imageBytes);

            Log.Information($"Successfully downloaded profile image to: {localPath}");
            return localPath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to download profile image from {imageUrl}: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Cleans a filename by removing invalid characters
    /// </summary>
    private static string CleanFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var invalidChar in invalidChars)
        {
            fileName = fileName.Replace(invalidChar, '_');
        }
        return fileName.Trim();
    }

    /// <summary>
    /// Extracts file extension from URL, defaults to .jpg
    /// </summary>
    private static string GetFileExtensionFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var extension = Path.GetExtension(path);
            return string.IsNullOrEmpty(extension) ? ".jpg" : extension;
        }
        catch
        {
            return ".jpg";
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
