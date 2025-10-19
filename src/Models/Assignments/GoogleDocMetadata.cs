using System;

namespace SchoolOrganizer.Src.Models.Assignments;

/// <summary>
/// Metadata for Google Docs files that have been downloaded
/// </summary>
public class GoogleDocMetadata
{
    /// <summary>
    /// Google Drive file ID
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// Original Google Docs title
    /// </summary>
    public string OriginalTitle { get; set; } = string.Empty;

    /// <summary>
    /// Google Drive web view link
    /// </summary>
    public string WebViewLink { get; set; } = string.Empty;

    /// <summary>
    /// Google Docs MIME type (e.g., application/vnd.google-apps.document)
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// Type of Google Doc: document, spreadsheet, presentation, drawing, etc.
    /// </summary>
    public string DocType { get; set; } = string.Empty;

    /// <summary>
    /// Path to the downloaded/converted file
    /// </summary>
    public string DownloadedFilePath { get; set; } = string.Empty;

    /// <summary>
    /// When the metadata was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the embed URL for displaying in WebView
    /// </summary>
    public string GetEmbedUrl()
    {
        if (string.IsNullOrEmpty(FileId))
            return string.Empty;

        return $"https://drive.google.com/file/d/{FileId}/preview";
    }

    /// <summary>
    /// Gets the edit URL for opening in browser
    /// </summary>
    public string GetEditUrl()
    {
        if (string.IsNullOrEmpty(FileId))
            return WebViewLink;

        return DocType switch
        {
            "document" => $"https://docs.google.com/document/d/{FileId}/edit",
            "spreadsheet" => $"https://docs.google.com/spreadsheets/d/{FileId}/edit",
            "presentation" => $"https://docs.google.com/presentation/d/{FileId}/edit",
            "drawing" => $"https://docs.google.com/drawings/d/{FileId}/edit",
            "form" => $"https://docs.google.com/forms/d/{FileId}/edit",
            _ => WebViewLink
        };
    }

    /// <summary>
    /// Determines the doc type from MIME type
    /// </summary>
    public static string GetDocTypeFromMimeType(string mimeType)
    {
        return mimeType switch
        {
            "application/vnd.google-apps.document" => "document",
            "application/vnd.google-apps.spreadsheet" => "spreadsheet",
            "application/vnd.google-apps.presentation" => "presentation",
            "application/vnd.google-apps.drawing" => "drawing",
            "application/vnd.google-apps.form" => "form",
            "application/vnd.google-apps.script" => "script",
            "application/vnd.google-apps.site" => "site",
            _ => "unknown"
        };
    }
}
