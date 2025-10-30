using System;

namespace SchoolOrganizer.Src.Services.Downloads.Models;

/// <summary>
/// Represents a single file entry in the download manifest
/// Tracks the relationship between submissions, files, and local storage
/// </summary>
public class FileManifestEntry
{
    /// <summary>
    /// Google Drive file ID
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// Submission ID this file belongs to
    /// </summary>
    public string SubmissionId { get; set; } = string.Empty;

    /// <summary>
    /// Student User ID
    /// </summary>
    public string StudentUserId { get; set; } = string.Empty;

    /// <summary>
    /// Course Work (Assignment) ID
    /// </summary>
    public string CourseWorkId { get; set; } = string.Empty;

    /// <summary>
    /// Local file path where the file is stored
    /// </summary>
    public string LocalPath { get; set; } = string.Empty;

    /// <summary>
    /// Drive file modification time (UTC) when last synced
    /// </summary>
    public DateTime? DriveModifiedTimeUtc { get; set; }

    /// <summary>
    /// Local file modification time (UTC) when last synced
    /// </summary>
    public DateTime LocalModifiedTimeUtc { get; set; }

    /// <summary>
    /// Whether this is a link/URL shortcut rather than an actual file
    /// </summary>
    public bool IsLink { get; set; }

    /// <summary>
    /// File name
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// When this entry was last updated
    /// </summary>
    public DateTime LastSyncedUtc { get; set; }
}

