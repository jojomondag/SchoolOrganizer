using System;
using System.Collections.Generic;

namespace SchoolOrganizer.Src.Services.Downloads.Models;

/// <summary>
/// Manifest for a course tracking all downloaded files and their metadata
/// </summary>
public class CourseManifest
{
    /// <summary>
    /// Course ID
    /// </summary>
    public string CourseId { get; set; } = string.Empty;

    /// <summary>
    /// Last sync time (UTC) for the entire course
    /// </summary>
    public DateTime LastSyncTimeUtc { get; set; }

    /// <summary>
    /// Dictionary mapping file IDs to their manifest entries
    /// </summary>
    public Dictionary<string, FileManifestEntry> Files { get; set; } = new();

    /// <summary>
    /// Dictionary mapping submission IDs to the file IDs they contain
    /// </summary>
    public Dictionary<string, HashSet<string>> SubmissionFiles { get; set; } = new();
}

