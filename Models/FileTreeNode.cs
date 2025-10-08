using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace SchoolOrganizer.Models;

/// <summary>
/// Represents a file tree node for the file explorer
/// </summary>
public class FileTreeNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string AssignmentName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsDirectory { get; set; }
    public string FileType { get; set; } = string.Empty;
    public ObservableCollection<FileTreeNode> Children { get; set; } = new();
    public Bitmap? ImageSource { get; set; }
    public string CodeContent { get; set; } = string.Empty;
    public string TextContent { get; set; } = string.Empty;
    public string FileExtension => Path.GetExtension(FullPath);
    public bool IsImage { get; set; }
    public bool IsCode { get; set; }
    public bool IsText { get; set; }
    public bool IsBinary { get; set; }
    public bool IsNone { get; set; }

    public async Task LoadContentAsync()
    {
        if (IsDirectory) return;

        try
        {
            var extension = Path.GetExtension(FullPath).ToLowerInvariant();
            Serilog.Log.Information("LoadContentAsync: Loading {FilePath} with extension {Extension}", FullPath, extension);
            
            if (IsImageFile(extension))
            {
                Serilog.Log.Information("LoadContentAsync: File {FileName} detected as IMAGE", Name);
                await LoadImageContent();
            }
            else if (IsCodeFile(extension))
            {
                Serilog.Log.Information("LoadContentAsync: File {FileName} detected as CODE", Name);
                await LoadCodeContent();
            }
            else if (IsTextFile(extension))
            {
                Serilog.Log.Information("LoadContentAsync: File {FileName} detected as TEXT", Name);
                await LoadTextContent();
            }
            else
            {
                Serilog.Log.Information("LoadContentAsync: File {FileName} detected as BINARY", Name);
                IsBinary = true;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Error loading content for file: {FilePath}", FullPath);
            IsNone = true;
        }
    }

    private bool IsImageFile(string extension)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg" };
        return Array.Exists(imageExtensions, ext => string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsCodeFile(string extension)
    {
        var codeExtensions = new[] { ".java", ".cs", ".cpp", ".c", ".h", ".py", ".js", ".html", ".css", ".xml" };
        return Array.Exists(codeExtensions, ext => string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsTextFile(string extension)
    {
        var textExtensions = new[] { ".txt", ".md", ".rtf" };
        return Array.Exists(textExtensions, ext => string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase));
    }

    private Task LoadImageContent()
    {
        try
        {
            using var stream = File.OpenRead(FullPath);
            ImageSource = new Bitmap(stream);
            IsImage = true;
        }
        catch
        {
            IsNone = true;
        }
        return Task.CompletedTask;
    }

    private async Task LoadCodeContent()
    {
        try
        {
            CodeContent = await File.ReadAllTextAsync(FullPath);
            IsCode = true;
            Serilog.Log.Information("LoadCodeContent: Successfully loaded {Length} characters for {FileName}, IsCode set to {IsCode}", 
                CodeContent.Length, Name, IsCode);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "LoadCodeContent: Failed to load code content for {FileName}", Name);
            IsNone = true;
        }
    }

    private async Task LoadTextContent()
    {
        try
        {
            TextContent = await File.ReadAllTextAsync(FullPath);
            IsText = true;
        }
        catch
        {
            IsNone = true;
        }
    }
}
