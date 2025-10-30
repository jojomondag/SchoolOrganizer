namespace SchoolOrganizer.Src.Services.Downloads.Utilities;

/// <summary>
/// Unified helper for MIME type conversions and file extensions
/// </summary>
public static class MimeTypeHelper
{
    /// <summary>
    /// Gets the export MIME type for Google Docs files
    /// </summary>
    public static string GetExportMimeType(string mimeType) => mimeType switch
    {
        "application/vnd.google-apps.document" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.google-apps.spreadsheet" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.google-apps.presentation" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/vnd.google-apps.drawing" => "image/png",
        "application/vnd.google-apps.script" => "application/vnd.google-apps.script+json",
        "application/vnd.google-apps.form" => "application/pdf",
        "application/vnd.google-apps.jam" => "application/pdf",
        "application/vnd.google-apps.site" => "text/html",
        "application/vnd.google-apps.folder" => "application/vnd.google-apps.folder",
        _ => "application/pdf",
    };

    /// <summary>
    /// Gets the file extension for a given MIME type
    /// </summary>
    public static string GetFileExtension(string mimeType) => mimeType switch
    {
        "application/pdf" => ".pdf",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
        "application/zip" => ".zip",
        "application/x-rar-compressed" => ".rar",
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        "text/plain" => ".txt",
        "text/csv" => ".csv",
        "application/vnd.ms-excel" => ".xls",
        "application/vnd.ms-powerpoint" => ".ppt",
        "application/msword" => ".doc",
        "application/vnd.google-apps.script+json" => ".json",
        "text/html" => ".html",
        "application/vnd.google-apps.folder" => "",
        _ => ".bin",
    };

    /// <summary>
    /// Checks if a file needs unpacking (ZIP or RAR)
    /// </summary>
    public static bool NeedsUnpacking(string mimeType)
    {
        return mimeType == "application/zip" || mimeType == "application/x-rar-compressed";
    }

    /// <summary>
    /// Checks if a MIME type is a Google Docs file
    /// </summary>
    public static bool IsGoogleDocsFile(string mimeType)
    {
        return mimeType.StartsWith("application/vnd.google-apps");
    }
}

