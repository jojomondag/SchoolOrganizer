using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Drive.v3;
using SchoolOrganizer.Src.Services.Downloads.Models;
using SchoolOrganizer.Src.Services.Downloads.Utilities;
using Serilog;

namespace SchoolOrganizer.Src.Services.Downloads.Files;

/// <summary>
/// Processes attachments from Google Classroom submissions
/// </summary>
public class AttachmentProcessor
{
    private readonly DriveService _driveService;
    private readonly FileDownloader _fileDownloader;

    public AttachmentProcessor(DriveService driveService, FileDownloader? fileDownloader = null)
    {
        _driveService = driveService ?? throw new ArgumentNullException(nameof(driveService));
        _fileDownloader = fileDownloader ?? new FileDownloader(driveService);
    }

    /// <summary>
    /// Processes a single attachment and downloads it if needed
    /// </summary>
    /// <param name="attachment">The attachment to process</param>
    /// <param name="assignmentDirectory">Directory where the attachment should be saved</param>
    /// <param name="submissionId">ID of the submission</param>
    /// <param name="studentName">Name of the student</param>
    /// <param name="assignmentName">Name of the assignment</param>
    /// <param name="existingFileIds">Set of file IDs already downloaded in this assignment directory</param>
    /// <param name="existingFileNames">Set of exact filenames (case-sensitive) already used in this assignment directory</param>
    /// <param name="updateStatus">Optional callback to update status messages</param>
    /// <param name="existingDriveModifiedTime">Existing Drive file modification time for change detection</param>
    /// <param name="existingLocalPath">Existing local file path for change detection</param>
    /// <returns>DownloadedFileInfo if successful, null otherwise</returns>
    public async Task<DownloadedFileInfo?> ProcessAttachmentAsync(
        Attachment attachment,
        string assignmentDirectory,
        string submissionId,
        string studentName,
        string assignmentName,
        HashSet<string>? existingFileIds = null,
        HashSet<string>? existingFileNames = null,
        Action<string>? updateStatus = null,
        DateTime? existingDriveModifiedTime = null,
        string? existingLocalPath = null)
    {
        try
        {
            if (attachment.DriveFile != null)
            {
                return await ProcessDriveFileAttachmentAsync(
                    attachment.DriveFile,
                    assignmentDirectory,
                    submissionId,
                    studentName,
                    assignmentName,
                    existingFileIds ?? new HashSet<string>(),
                    existingFileNames ?? new HashSet<string>(),
                    updateStatus,
                    existingDriveModifiedTime,
                    existingLocalPath);
            }
            else if (attachment.Link != null)
            {
                return await ProcessLinkAttachmentAsync(
                    attachment.Link,
                    assignmentDirectory,
                    submissionId,
                    studentName,
                    assignmentName,
                    updateStatus);
            }

            return null;
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error processing attachment for {studentName} - {assignmentName}: {ex.Message}";
            updateStatus?.Invoke(errorMessage);
            Log.Error(ex, errorMessage);
            return null;
        }
    }

    private async Task<DownloadedFileInfo?> ProcessDriveFileAttachmentAsync(
        DriveFile driveFile,
        string assignmentDirectory,
        string submissionId,
        string studentName,
        string assignmentName,
        HashSet<string> existingFileIds,
        HashSet<string> existingFileNames,
        Action<string>? updateStatus,
        DateTime? existingDriveModifiedTime = null,
        string? existingLocalPath = null)
    {
        try
        {
            string fileName = DirectoryUtil.SanitizeFolderName(driveFile.Title ?? "File");
            
            // Fetch file metadata to determine MIME type and modification time
            var file = await _driveService.Files.Get(driveFile.Id).ExecuteAsync();
            string mimeType = file.MimeType ?? "";
            DateTime? driveModifiedTime = file.ModifiedTimeDateTimeOffset?.UtcDateTime;

            // Check if file needs updating (only for incremental sync)
            bool needsDownload = true;
            if (existingDriveModifiedTime.HasValue && driveModifiedTime.HasValue && existingLocalPath != null)
            {
                // File exists and modification times match - skip download
                if (driveModifiedTime.Value <= existingDriveModifiedTime.Value && File.Exists(existingLocalPath))
                {
                    needsDownload = false;
                    Log.Debug($"File {driveFile.Id} unchanged, skipping download");
                }
            }

            // Check if the file needs to be unpacked (ZIP/RAR) - create URL shortcut instead
            if (MimeTypeHelper.NeedsUnpacking(mimeType))
            {
                string linkPath = Path.Combine(assignmentDirectory, fileName + ".url");
                
                // Only update if it doesn't exist or is different
                if (needsDownload || !File.Exists(linkPath))
                {
                    await File.WriteAllTextAsync(linkPath, $"[InternetShortcut]\nURL=https://drive.google.com/file/d/{driveFile.Id}/view");
                    updateStatus?.Invoke($"Updated link: {driveFile.Title} for {studentName} - {assignmentName}");
                }
                
                return new DownloadedFileInfo(
                    driveFile.Id,
                    driveFile.Title ?? "File",
                    linkPath,
                    DateTime.UtcNow,
                    studentName,
                    assignmentName,
                    true);
            }

            // Download the file using intelligent naming strategy
            string filePath = needsDownload || existingLocalPath == null
                ? await _fileDownloader.DownloadFileAsync(
                    driveFile.Id,
                    driveFile.Title ?? "Untitled",
                    mimeType,
                    assignmentDirectory,
                    existingFileIds,
                    existingFileNames)
                : existingLocalPath;

            if (needsDownload)
            {
                string successMessage = $"Downloaded: {Path.GetFileName(filePath)} for {studentName} - {assignmentName}";
                updateStatus?.Invoke(successMessage);
            }

            return new DownloadedFileInfo(
                driveFile.Id,
                Path.GetFileName(filePath),
                filePath,
                DateTime.UtcNow,
                studentName,
                assignmentName,
                false);
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error downloading file for {studentName} - {assignmentName}: {ex.Message}";
            updateStatus?.Invoke(errorMessage);
            Log.Error(ex, errorMessage);
            return null;
        }
    }

    private async Task<DownloadedFileInfo?> ProcessLinkAttachmentAsync(
        Link link,
        string assignmentDirectory,
        string submissionId,
        string studentName,
        string assignmentName,
        Action<string>? updateStatus)
    {
        try
        {
            string statusMessage = $"Saving link: {link.Title} for {studentName} - {assignmentName}";
            updateStatus?.Invoke(statusMessage);

            string linkPath = Path.Combine(
                assignmentDirectory,
                DirectoryUtil.SanitizeFolderName(link.Title ?? "Link") + ".url");
            
            await File.WriteAllTextAsync(linkPath, $"[InternetShortcut]\nURL={link.Url}");

            return new DownloadedFileInfo(
                link.Url,
                link.Title ?? "Link",
                linkPath,
                DateTime.UtcNow,
                studentName,
                assignmentName,
                true);
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error saving link for {studentName} - {assignmentName}: {ex.Message}";
            updateStatus?.Invoke(errorMessage);
            Log.Error(ex, errorMessage);
            return null;
        }
    }
}

