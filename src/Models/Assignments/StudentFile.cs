using System;
using System.ComponentModel;
using System.IO;
using Avalonia.Media.Imaging;

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
}
