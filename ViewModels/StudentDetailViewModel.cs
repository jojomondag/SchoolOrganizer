using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace SchoolOrganizer.ViewModels;

public partial class StudentDetailViewModel : ObservableObject
{
    [ObservableProperty]
    private string _studentName = string.Empty;

    [ObservableProperty]
    private string _folderPath = string.Empty;

    [ObservableProperty]
    private string _statusText = "Loading...";

    [ObservableProperty]
    private FileItem? _selectedFile;

    [ObservableProperty]
    private string? _fileContent;

    [ObservableProperty]
    private string? _fileExtension;

    public ObservableCollection<AssignmentFolder> Assignments { get; } = new();

    public StudentDetailViewModel(string studentName, string folderPath)
    {
        _studentName = studentName;
        _folderPath = folderPath;
        Task.Run(LoadStudentFilesAsync);
    }

    private Task LoadStudentFilesAsync()
    {
        try
        {
            if (!Directory.Exists(FolderPath))
            {
                StatusText = "Folder not found";
                return Task.CompletedTask;
            }

            var assignmentDirs = Directory.GetDirectories(FolderPath);

            foreach (var assignmentDir in assignmentDirs)
            {
                var assignmentName = Path.GetFileName(assignmentDir);
                var assignment = new AssignmentFolder { Name = assignmentName };

                var files = Directory.GetFiles(assignmentDir, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    assignment.Files.Add(new FileItem
                    {
                        Name = fileInfo.Name,
                        Path = file,
                        Size = fileInfo.Length,
                        Extension = fileInfo.Extension,
                        LastModified = fileInfo.LastWriteTime
                    });
                }

                Assignments.Add(assignment);
            }

            StatusText = $"Loaded {Assignments.Count} assignments";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading student files");
            StatusText = $"Error: {ex.Message}";
        }
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ViewFile(FileItem? file)
    {
        if (file == null) return;

        try
        {
            SelectedFile = file;
            FileExtension = file.Extension;

            if (IsTextFile(file.Extension))
            {
                FileContent = await File.ReadAllTextAsync(file.Path);
            }
            else
            {
                FileContent = $"[Binary file: {file.Name}]\nSize: {file.Size} bytes\nUse 'Open in Explorer' to view this file.";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error viewing file");
            FileContent = $"Error loading file: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenInExplorer(FileItem? file)
    {
        if (file == null || !File.Exists(file.Path))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{file.Path}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening file in explorer");
        }
    }

    private bool IsTextFile(string extension)
    {
        var textExtensions = new[] { ".txt", ".cs", ".java", ".py", ".js", ".html", ".css", ".xml", ".json", ".md", ".cpp", ".c", ".h" };
        return textExtensions.Contains(extension.ToLowerInvariant());
    }
}

public class AssignmentFolder
{
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<FileItem> Files { get; } = new();
}

public class FileItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Extension { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}
