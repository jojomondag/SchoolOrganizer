using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Classroom.v1.Data;
using SchoolOrganizer.Src.Services.Downloads.Files;
using SchoolOrganizer.Src.Services.Downloads.Utilities;
using Serilog;

namespace SchoolOrganizer.Src.Services.Downloads.Core;

/// <summary>
/// Shared processor for handling student submissions and attachments
/// </summary>
public class SubmissionProcessor
{
    private readonly AttachmentProcessor _attachmentProcessor;
    private readonly SemaphoreSlim? _semaphore;

    public SubmissionProcessor(AttachmentProcessor attachmentProcessor, SemaphoreSlim? semaphore = null)
    {
        _attachmentProcessor = attachmentProcessor ?? throw new ArgumentNullException(nameof(attachmentProcessor));
        _semaphore = semaphore;
    }

    /// <summary>
    /// Processes submissions for a student and downloads all attachments
    /// </summary>
    public async Task<bool> ProcessSubmissionsAsync(
        List<StudentSubmission> submissions,
        Student student,
        Dictionary<string, CourseWork> courseWorkDict,
        string studentDirectory,
        Action<string>? updateStatus = null)
    {
        var tasks = new List<Task<bool>>();
        var processedAttachments = new HashSet<string>();
        
        // Track files per assignment directory to handle duplicates intelligently
        // We track both file IDs (for same file detection) and filenames (for exact duplicate detection)
        var assignmentFileIds = new Dictionary<string, HashSet<string>>();
        var assignmentFileNames = new Dictionary<string, HashSet<string>>();

        foreach (var submission in submissions)
        {
            if (submission.AssignmentSubmission?.Attachments == null)
                continue;

            var courseWork = courseWorkDict.GetValueOrDefault(submission.CourseWorkId);
            string studentName = student.Profile.Name.FullName;
            string assignmentName = courseWork?.Title ?? "Unknown Assignment";
            string assignmentDirectory = DirectoryUtil.CreateAssignmentDirectory(studentDirectory, new CourseWork { Title = assignmentName });

            // Get or create the file ID set for this assignment directory
            if (!assignmentFileIds.TryGetValue(assignmentDirectory, out var existingFileIds))
            {
                existingFileIds = new HashSet<string>();
                assignmentFileIds[assignmentDirectory] = existingFileIds;
            }
            
            // Get or create the filename set for this assignment directory (case-sensitive tracking)
            if (!assignmentFileNames.TryGetValue(assignmentDirectory, out var existingFileNames))
            {
                existingFileNames = new HashSet<string>();
                assignmentFileNames[assignmentDirectory] = existingFileNames;
            }

            var attachmentList = submission.AssignmentSubmission.Attachments.ToList();

            // Process each attachment
            foreach (var attachment in attachmentList)
            {
                string attachmentId = attachment.DriveFile?.Id ?? attachment.Link?.Url ?? "";
                if (!string.IsNullOrEmpty(attachmentId) && !processedAttachments.Contains(attachmentId))
                {
                    processedAttachments.Add(attachmentId);
                    
                    if (_semaphore != null)
                    {
                        // With semaphore (for DownloadManager - parallel downloads with limit)
                        tasks.Add(ProcessAttachmentWithSemaphoreAsync(
                            attachment,
                            assignmentDirectory,
                            submission.Id,
                            studentName,
                            assignmentName,
                            existingFileIds,
                            existingFileNames,
                            updateStatus));
                    }
                    else
                    {
                        // Without semaphore (for StudentFileDownloader - full parallelization)
                        tasks.Add(ProcessAttachmentAsync(
                            attachment,
                            assignmentDirectory,
                            submission.Id,
                            studentName,
                            assignmentName,
                            existingFileIds,
                            existingFileNames,
                            updateStatus));
                    }
                }
            }
        }

        var results = await Task.WhenAll(tasks);
        return results.Any(r => r);
    }

    private async Task<bool> ProcessAttachmentWithSemaphoreAsync(
        Attachment attachment,
        string assignmentDirectory,
        string submissionId,
        string studentName,
        string assignmentName,
        HashSet<string> existingFileIds,
        HashSet<string> existingFileNames,
        Action<string>? updateStatus)
    {
        await _semaphore!.WaitAsync();
        try
        {
            var fileInfo = await _attachmentProcessor.ProcessAttachmentAsync(
                attachment,
                assignmentDirectory,
                submissionId,
                studentName,
                assignmentName,
                existingFileIds,
                existingFileNames,
                updateStatus);
            
            // Track downloaded file ID (filename is already tracked in FileNamingStrategy)
            if (fileInfo != null && attachment.DriveFile != null)
            {
                existingFileIds.Add(attachment.DriveFile.Id);
                // Note: Filename tracking happens in FileNamingStrategy.GetFilePathForDownload
                // which adds the filename to existingFileNames when determining the path
            }
            
            return fileInfo != null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<bool> ProcessAttachmentAsync(
        Attachment attachment,
        string assignmentDirectory,
        string submissionId,
        string studentName,
        string assignmentName,
        HashSet<string> existingFileIds,
        HashSet<string> existingFileNames,
        Action<string>? updateStatus)
    {
        var fileInfo = await _attachmentProcessor.ProcessAttachmentAsync(
            attachment,
            assignmentDirectory,
            submissionId,
            studentName,
            assignmentName,
            existingFileIds,
            existingFileNames,
            updateStatus);
        
        // Track downloaded file ID (filename is already tracked in FileNamingStrategy)
        if (fileInfo != null && attachment.DriveFile != null)
        {
            lock (existingFileIds)
            {
                existingFileIds.Add(attachment.DriveFile.Id);
                // Note: Filename tracking happens in FileNamingStrategy.GetFilePathForDownload
                // which adds the filename to existingFileNames when determining the path
            }
        }
        
        return fileInfo != null;
    }
}

