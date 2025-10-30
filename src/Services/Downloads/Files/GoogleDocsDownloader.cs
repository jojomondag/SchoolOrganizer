using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using SchoolOrganizer.Src.Models.Assignments;
using SchoolOrganizer.Src.Services.Downloads.Utilities;
using Serilog;

namespace SchoolOrganizer.Src.Services.Downloads.Files;

/// <summary>
/// Handles downloading and processing Google Docs files
/// </summary>
public class GoogleDocsDownloader
{
    private readonly DriveService _driveService;

    public GoogleDocsDownloader(DriveService driveService)
    {
        _driveService = driveService ?? throw new ArgumentNullException(nameof(driveService));
    }

    /// <summary>
    /// Downloads a Google Docs file and exports it to the appropriate format
    /// </summary>
    public async Task<string> DownloadGoogleDocAsync(
        string fileId,
        string fileName,
        string mimeType,
        string destinationFolder,
        HashSet<string>? existingFileIds = null,
        HashSet<string>? existingFileNames = null)
    {
        try
        {
            // Ensure the destination directory exists (CreateDirectory handles existence automatically)
            Directory.CreateDirectory(destinationFolder);

            existingFileIds ??= new HashSet<string>();
            existingFileNames ??= new HashSet<string>();

            // Get export MIME type and file extension
            var exportMimeType = MimeTypeHelper.GetExportMimeType(mimeType);
            var fileExtension = MimeTypeHelper.GetFileExtension(exportMimeType);
            
            // Build filename with proper extension for naming strategy
            string fileNameWithExtension = Path.GetFileNameWithoutExtension(fileName) + fileExtension;

            // Use intelligent naming strategy to determine file path (only creates folders for exact duplicates)
            string filePath = FileNamingStrategy.GetFilePathForDownload(fileId, fileNameWithExtension, destinationFolder, existingFileIds, existingFileNames);
            
            // Ensure the directory exists
            string fileFolder = Path.GetDirectoryName(filePath) ?? destinationFolder;
            Directory.CreateDirectory(fileFolder);

            // Check if this is the same file (via metadata) - if so, we can overwrite
            if (File.Exists(filePath))
            {
                string metadataPath = filePath + ".gdocmeta.json";
                if (File.Exists(metadataPath))
                {
                    try
                    {
                        var metadataJson = await File.ReadAllTextAsync(metadataPath);
                        var metadata = System.Text.Json.JsonSerializer.Deserialize<GoogleDocMetadata>(metadataJson);
                        if (metadata?.FileId == fileId)
                        {
                            // Same file - overwrite is fine
                            Log.Information($"Same Google Doc detected, overwriting: {filePath}");
                        }
                    }
                    catch
                    {
                        // Metadata read failed - file path already determined by FileNamingStrategy
                    }
                }
            }

            // Download the file (overwrite if exists - same file ID means same file)
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            await _driveService.Files.Export(fileId, exportMimeType).DownloadAsync(fileStream);
            Log.Information($"Downloaded Google Doc: {filePath}");

            // Save Google Docs metadata
            await SaveGoogleDocMetadataAsync(fileId, fileName, mimeType, filePath);

            return filePath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error downloading Google Doc {fileName}");
            throw;
        }
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

            Log.Information($"Saved Google Docs metadata: {metadataPath}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error saving Google Docs metadata for {fileName}");
        }
    }
}

