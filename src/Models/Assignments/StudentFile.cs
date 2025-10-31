using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Serilog;

namespace SchoolOrganizer.Src.Models.Assignments;

/// <summary>
/// Represents a student file with metadata
/// </summary>
public class StudentFile : INotifyPropertyChanged
{
    private Bitmap? _imageSource;
    private string _codeContent = string.Empty;
    private string _textContent = string.Empty;
    private bool _isImage;
    private bool _isCode;
    private bool _isText;
    private bool _isBinary;
    private bool _isNone;
    private bool _isGoogleDoc;
    private string _googleDocUrl = string.Empty;
    private string _googleDocEmbedUrl = string.Empty;
    private bool _showEmbeddedGoogleDoc = true;
    private bool _isExpanded = false; // Default to collapsed state

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string AssignmentName { get; set; } = string.Empty;
    public GoogleDocMetadata? GoogleDocMetadata { get; set; }
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string FileSizeFormatted => FormatFileSize(FileSize);
    public string LastModifiedFormatted => LastModified.ToString("yyyy-MM-dd HH:mm");
    public string FileExtension 
    { 
        get 
        {
            var ext = Path.GetExtension(FilePath);
            if (string.IsNullOrEmpty(ext)) ext = "";
            return ext;
        }
    }

    public Bitmap? ImageSource
    {
        get => _imageSource;
        set
        {
            _imageSource = value;
            OnPropertyChanged();
        }
    }

    public string CodeContent
    {
        get => _codeContent;
        set
        {
            _codeContent = value;
            OnPropertyChanged();
        }
    }

    public string TextContent
    {
        get => _textContent;
        set
        {
            _textContent = value;
            OnPropertyChanged();
        }
    }

    public bool IsImage
    {
        get => _isImage;
        set
        {
            _isImage = value;
            OnPropertyChanged();
        }
    }

    public bool IsCode
    {
        get => _isCode;
        set
        {
            _isCode = value;
            OnPropertyChanged();
        }
    }

    public bool IsText
    {
        get => _isText;
        set
        {
            _isText = value;
            OnPropertyChanged();
        }
    }

    public bool IsBinary
    {
        get => _isBinary;
        set
        {
            _isBinary = value;
            OnPropertyChanged();
        }
    }

    public bool IsNone
    {
        get => _isNone;
        set
        {
            _isNone = value;
            OnPropertyChanged();
        }
    }

    public bool IsGoogleDoc
    {
        get => _isGoogleDoc;
        set
        {
            _isGoogleDoc = value;
            OnPropertyChanged();
        }
    }

    public string GoogleDocUrl
    {
        get => _googleDocUrl;
        set
        {
            _googleDocUrl = value;
            OnPropertyChanged();
        }
    }

    public string GoogleDocEmbedUrl
    {
        get => _googleDocEmbedUrl;
        set
        {
            _googleDocEmbedUrl = value;
            OnPropertyChanged();
        }
    }

    public bool ShowEmbeddedGoogleDoc
    {
        get => _showEmbeddedGoogleDoc;
        set
        {
            _showEmbeddedGoogleDoc = value;
            OnPropertyChanged();
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public async Task LoadContentAsync()
    {
        try
        {
            var extension = Path.GetExtension(FilePath).ToLowerInvariant();

            if (IsImageFile(extension))
            {
                await LoadImageContent();
            }
            else if (IsCodeFile(extension))
            {
                await LoadCodeContent();
            }
            else if (IsTextFile(extension))
            {
                await LoadTextContent();
            }
            else if (IsDocumentFile(extension))
            {
                // Document files (docx, xlsx, pptx, pdf) - show as "none" rather than binary
                // This allows them to be handled specially in the UI
                IsNone = true;
            }
            else
            {
                IsBinary = true;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error loading content for file: {FilePath}", FilePath);
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

    private bool IsDocumentFile(string extension)
    {
        var documentExtensions = new[] { ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".pdf" };
        return Array.Exists(documentExtensions, ext => string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase));
    }

    private Task LoadImageContent()
    {
        try
        {
            using var stream = File.OpenRead(FilePath);
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
            CodeContent = await File.ReadAllTextAsync(FilePath);
            IsCode = true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load code content for {FileName}", FileName);
            IsNone = true;
        }
    }

    private async Task LoadTextContent()
    {
        try
        {
            TextContent = await File.ReadAllTextAsync(FilePath);
            IsText = true;
        }
        catch
        {
            IsNone = true;
        }
    }
}
