using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using SchoolOrganizer.Src.Services.Downloads.Utilities;
using Serilog;

namespace SchoolOrganizer.Src.Services.Downloads.Files;

/// <summary>
/// Unified file downloader for regular files and Google Docs
/// </summary>
public class FileDownloader
{
    private readonly DriveService _driveService;
    private readonly GoogleDocsDownloader _googleDocsDownloader;

    public FileDownloader(DriveService driveService)
    {
        _driveService = driveService ?? throw new ArgumentNullException(nameof(driveService));
        _googleDocsDownloader = new GoogleDocsDownloader(driveService);
    }

    /// <summary>
    /// Downloads a file from Google Drive to the specified destination folder
    /// </summary>
    /// <param name="fileId">Google Drive file ID</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="mimeType">MIME type of the file</param>
    /// <param name="destinationFolder">Destination folder path</param>
    /// <param name="existingFileIds">Set of file IDs already downloaded in this folder (for duplicate handling)</param>
    /// <param name="existingFileNames">Set of exact filenames (case-sensitive) already used in this folder</param>
    /// <returns>Path to the downloaded file</returns>
    public async Task<string> DownloadFileAsync(
        string fileId,
        string fileName,
        string mimeType,
        string destinationFolder,
        HashSet<string>? existingFileIds = null,
        HashSet<string>? existingFileNames = null)
    {
        try
        {
            existingFileIds ??= new HashSet<string>();
            existingFileNames ??= new HashSet<string>();

            // Handle Google Docs files
            if (MimeTypeHelper.IsGoogleDocsFile(mimeType))
            {
                return await _googleDocsDownloader.DownloadGoogleDocAsync(fileId, fileName, mimeType, destinationFolder, existingFileIds, existingFileNames);
            }

            // Handle regular files - use intelligent naming strategy
            // First ensure we have a proper file extension
            string fileExtension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(fileExtension))
            {
                fileExtension = MimeTypeHelper.GetFileExtension(mimeType);
            }
            
            // Build filename with extension for naming strategy
            string fileNameWithExtension = Path.GetFileNameWithoutExtension(fileName) + fileExtension;
            string filePath = FileNamingStrategy.GetFilePathForDownload(fileId, fileNameWithExtension, destinationFolder, existingFileIds, existingFileNames);
            
            // Ensure the directory exists
            string fileFolder = Path.GetDirectoryName(filePath) ?? destinationFolder;
            Directory.CreateDirectory(fileFolder);

            // Download the file (overwrite if exists - same file ID means same file)
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            await _driveService.Files.Get(fileId).DownloadAsync(fileStream);

            Log.Information($"Downloaded file: {filePath}");
            return filePath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error downloading file {fileName}");
            throw;
        }
    }
}

