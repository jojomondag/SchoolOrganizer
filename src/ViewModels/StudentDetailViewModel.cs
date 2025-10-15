using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Serilog;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using System.Text;
using Avalonia.Controls;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Threading;
using SchoolOrganizer.Src.Services;
using SchoolOrganizer.Src.Models.Students;
using SchoolOrganizer.Src.Models.Assignments;
using SchoolOrganizer.Src.Models.UI;
using Avalonia.ReactiveUI;

namespace SchoolOrganizer.Src.ViewModels;

/// <summary>
/// ViewModel for displaying a student's downloaded assignments
/// </summary>
public class StudentDetailViewModel : ReactiveObject
{
    private string _studentName = string.Empty;
    private string _courseName = string.Empty;
    private string _studentFolderPath = string.Empty;
    private bool _isLoading;
    private string _statusText = string.Empty;
    private ObservableCollection<StudentFile> _studentFiles = new();
    private ObservableCollection<FileTreeNode> _fileTree = new();
    private FileTreeNode? _selectedFile;
    private ObservableCollection<FileTreeNode> _folderFiles = new();
    private ObservableCollection<AssignmentGroup> _allFilesGrouped = new();
    private FileViewerScrollService? _scrollService;
    
    // Navigation properties
    private string _selectedAssignment = string.Empty;
    private string _selectedViewMode = "AllFiles";
    private bool _isNavigationOpen = true;

    public string StudentName
    {
        get => _studentName;
        set 
        { 
            this.RaiseAndSetIfChanged(ref _studentName, value);
            UpdateWindowTitle();
        }
    }

    public string CourseName
    {
        get => _courseName;
        set 
        { 
            this.RaiseAndSetIfChanged(ref _courseName, value);
            UpdateWindowTitle();
        }
    }

    public string WindowTitle => "View Assignments";

    private void UpdateWindowTitle()
    {
        this.RaisePropertyChanged(nameof(WindowTitle));
    }

    public string StudentFolderPath
    {
        get => _studentFolderPath;
        set => this.RaiseAndSetIfChanged(ref _studentFolderPath, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public ObservableCollection<StudentFile> StudentFiles
    {
        get => _studentFiles;
        set => this.RaiseAndSetIfChanged(ref _studentFiles, value);
    }

    public ObservableCollection<FileTreeNode> FileTree
    {
        get => _fileTree;
        set => this.RaiseAndSetIfChanged(ref _fileTree, value);
    }

    public FileTreeNode? SelectedFile
    {
        get => _selectedFile;
        set 
        { 
            this.RaiseAndSetIfChanged(ref _selectedFile, value);
            if (value != null)
            {
                LoadFolderFiles(value);
            }
        }
    }

    public ObservableCollection<FileTreeNode> FolderFiles
    {
        get => _folderFiles;
        set => this.RaiseAndSetIfChanged(ref _folderFiles, value);
    }

    public ObservableCollection<AssignmentGroup> AllFilesGrouped
    {
        get => _allFilesGrouped;
        set => this.RaiseAndSetIfChanged(ref _allFilesGrouped, value);
    }

    // Navigation properties
    public string SelectedAssignment
    {
        get => _selectedAssignment;
        set => this.RaiseAndSetIfChanged(ref _selectedAssignment, value);
    }

    public string SelectedViewMode
    {
        get => _selectedViewMode;
        set => this.RaiseAndSetIfChanged(ref _selectedViewMode, value);
    }

    public bool IsNavigationOpen
    {
        get => _isNavigationOpen;
        set => this.RaiseAndSetIfChanged(ref _isNavigationOpen, value);
    }


    public ReactiveCommand<StudentFile, Unit> OpenFileCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSelectedFileCommand { get; }
    
    // Navigation commands - removed to avoid threading issues, using event handlers instead

    /// <summary>
    /// Sets the scroll service for this view model
    /// </summary>
    public void SetScrollService(FileViewerScrollService scrollService)
    {
        _scrollService = scrollService;
    }

    /// <summary>
    /// Scrolls to a specific assignment group by name
    /// </summary>
    public async Task<bool> ScrollToAssignmentAsync(string assignmentName)
    {
        if (_scrollService == null)
        {
            Log.Warning("Cannot scroll: scroll service not initialized");
            return false;
        }

        // Find a node that represents this assignment
        var targetNode = FindAssignmentNode(assignmentName);
        if (targetNode == null)
        {
            Log.Warning("Cannot find assignment node for: {AssignmentName}", assignmentName);
            return false;
        }

        return await _scrollService.ScrollToAssignmentGroupAsync(targetNode, AllFilesGrouped);
    }

    /// <summary>
    /// Finds a file tree node that represents the given assignment
    /// </summary>
    private FileTreeNode? FindAssignmentNode(string assignmentName)
    {
        // Search through the file tree for a node that matches the assignment
        return FindAssignmentNodeRecursive(FileTree, assignmentName);
    }

    private FileTreeNode? FindAssignmentNodeRecursive(IEnumerable<FileTreeNode> nodes, string assignmentName)
    {
        foreach (var node in nodes)
        {
            // Check if this node represents the assignment
            if (string.Equals(node.AssignmentName, assignmentName, StringComparison.OrdinalIgnoreCase) ||
                (node.IsDirectory && string.Equals(node.Name, assignmentName, StringComparison.OrdinalIgnoreCase)))
            {
                return node;
            }

            // Recursively search children
            var found = FindAssignmentNodeRecursive(node.Children, assignmentName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    public StudentDetailViewModel()
    {
        OpenFileCommand = ReactiveCommand.Create<StudentFile>(file => 
        {
            try
            {
                if (file != null)
                {
                    OpenFile(file);
                }
                else
                {
                    Log.Warning("OpenFileCommand called with null file parameter");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception in OpenFileCommand: {Message}", ex.Message);
            }
        });
        CloseCommand = ReactiveCommand.Create(() => { });
        OpenSelectedFileCommand = ReactiveCommand.Create(OpenSelectedFile);
        
        // Navigation commands removed - using event handlers in code-behind to avoid threading issues
    }

    /// <summary>
    /// Loads student files from the specified folder
    /// </summary>
    public async Task LoadStudentFilesAsync(string studentName, string courseName, string studentFolderPath)
    {
        StudentName = studentName;
        CourseName = courseName;
        StudentFolderPath = studentFolderPath;
        IsLoading = true;
        StatusText = "Loading student files...";

        try
        {
            if (!Directory.Exists(studentFolderPath))
            {
                StatusText = $"Student folder not found: {studentFolderPath}";
                Log.Warning("Student folder does not exist: {FolderPath}", studentFolderPath);
                return;
            }

            var files = new List<StudentFile>();
            await LoadFilesRecursively(studentFolderPath, files, studentFolderPath);

            StudentFiles = new ObservableCollection<StudentFile>(files.OrderBy(f => f.AssignmentName).ThenBy(f => f.FileName));
            
            // Build file tree
            var fileTree = BuildFileTree(studentFolderPath, studentFolderPath);
            FileTree = new ObservableCollection<FileTreeNode>(fileTree);
            
            // Build grouped files for the main scroll view
            var groupedFiles = BuildGroupedFiles(files);
            AllFilesGrouped = new ObservableCollection<AssignmentGroup>(groupedFiles);
            
            // Load content for all files to enable preview
            await LoadContentForAllFilesAsync();
            
            // Automatically select the first file and load its content
            if (fileTree.Count > 0)
            {
                var firstFile = fileTree.FirstOrDefault();
                if (firstFile != null)
                {
                    SelectedFile = firstFile;
                    await firstFile.LoadContentAsync();
                }
            }
            else
            {
                // If no files in tree, show all files directly
                var allFiles = new ObservableCollection<FileTreeNode>();
                foreach (var file in files)
                {
                    var fileNode = new FileTreeNode
                    {
                        Name = file.FileName,
                        FullPath = file.FilePath,
                        RelativePath = file.RelativePath,
                        AssignmentName = file.AssignmentName,
                        FileSize = file.FileSize,
                        LastModified = file.LastModified,
                        IsDirectory = false,
                        FileType = GetFileType(Path.GetExtension(file.FilePath)),
                        Children = new ObservableCollection<FileTreeNode>()
                    };
                    await fileNode.LoadContentAsync();
                    allFiles.Add(fileNode);
                }
                FolderFiles = allFiles;
            }
            
            StatusText = $"Loaded {files.Count} files for {studentName}";
            Log.Information("Successfully loaded {FileCount} files for {StudentName}", files.Count, studentName);
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading files: {ex.Message}";
            Log.Error(ex, "Error loading student files for {StudentName}", studentName);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadFilesRecursively(string directoryPath, List<StudentFile> files, string basePath)
    {
        try
        {
            var directory = new DirectoryInfo(directoryPath);
            
            foreach (var file in directory.GetFiles())
            {
                var relativePath = Path.GetRelativePath(basePath, file.FullName);
                var assignmentName = GetAssignmentNameFromPath(relativePath);
                
                files.Add(new StudentFile
                {
                    FileName = file.Name,
                    FilePath = file.FullName,
                    AssignmentName = assignmentName,
                    FileSize = file.Length,
                    LastModified = file.LastWriteTime,
                    RelativePath = relativePath
                });
            }

            foreach (var subdirectory in directory.GetDirectories())
            {
                await LoadFilesRecursively(subdirectory.FullName, files, basePath);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error loading files from directory {DirectoryPath}", directoryPath);
        }
    }

    private string GetAssignmentNameFromPath(string relativePath)
    {
        var pathParts = relativePath.Split(Path.DirectorySeparatorChar);
        if (pathParts.Length > 1)
        {
            return pathParts[0];
        }
        // For assignment folders (direct children), use the folder name itself
        if (pathParts.Length == 1 && !string.IsNullOrEmpty(pathParts[0]))
        {
            return pathParts[0];
        }
        return "General";
    }

    private List<FileTreeNode> BuildFileTree(string directoryPath, string basePath)
    {
        var nodes = new List<FileTreeNode>();
        
        try
        {
            var directory = new DirectoryInfo(directoryPath);
            
            // Add files in current directory
            foreach (var file in directory.GetFiles().OrderBy(f => f.Name))
            {
                var relativePath = Path.GetRelativePath(basePath, file.FullName);
                var assignmentName = GetAssignmentNameFromPath(relativePath);
                
                nodes.Add(new FileTreeNode
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    RelativePath = relativePath,
                    AssignmentName = assignmentName,
                    FileSize = file.Length,
                    LastModified = file.LastWriteTime,
                    IsDirectory = false,
                    FileType = GetFileType(file.Extension),
                    Children = new ObservableCollection<FileTreeNode>()
                });
            }
            
            // Add subdirectories
            foreach (var subdirectory in directory.GetDirectories().OrderBy(d => d.Name))
            {
                var relativePath = Path.GetRelativePath(basePath, subdirectory.FullName);
                var assignmentName = GetAssignmentNameFromPath(relativePath);
                
                var dirNode = new FileTreeNode
                {
                    Name = subdirectory.Name,
                    FullPath = subdirectory.FullName,
                    RelativePath = relativePath,
                    AssignmentName = assignmentName,
                    FileSize = 0,
                    LastModified = subdirectory.LastWriteTime,
                    IsDirectory = true,
                    FileType = "Folder",
                    Children = new ObservableCollection<FileTreeNode>(BuildFileTree(subdirectory.FullName, basePath))
                };
                
                nodes.Add(dirNode);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error building file tree for {DirectoryPath}", directoryPath);
        }
        
        return nodes;
    }

    private string GetFileType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".svg" => "Image",
            ".java" or ".cs" or ".cpp" or ".c" or ".h" or ".py" or ".js" or ".html" or ".css" or ".xml" => "Code",
            ".txt" or ".md" or ".rtf" => "Text",
            ".pdf" => "PDF",
            ".doc" or ".docx" => "Document",
            ".zip" or ".rar" or ".7z" => "Archive",
            _ => "File"
        };
    }

    public void OpenFile(StudentFile file)
    {
        if (file == null || string.IsNullOrEmpty(file.FilePath))
        {
            StatusText = "No file selected or file path is empty.";
            return;
        }

        if (!System.IO.File.Exists(file.FilePath))
        {
            StatusText = $"File not found: {file.FilePath}";
            return;
        }

        try
        {
            var fileHandlingService = new FileHandlingService();
            var success = fileHandlingService.OpenFile(file);
            
            if (success)
            {
                StatusText = $"Opened {file.FileName}";
            }
            else
            {
                StatusText = $"Failed to open {file.FileName}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening file: {ex.Message}";
            Log.Error(ex, "Error opening file: {FilePath}", file.FilePath);
        }
    }


    private void OpenSelectedFile()
    {
        if (SelectedFile != null && !SelectedFile.IsDirectory)
        {
            OpenFile(new StudentFile
            {
                FileName = SelectedFile.Name,
                FilePath = SelectedFile.FullPath,
                AssignmentName = SelectedFile.AssignmentName,
                FileSize = SelectedFile.FileSize,
                LastModified = SelectedFile.LastModified,
                RelativePath = SelectedFile.RelativePath
            });
        }
    }

    private async void LoadFolderFiles(FileTreeNode selectedNode)
    {
        var folderFiles = new ObservableCollection<FileTreeNode>();
        
        if (selectedNode.IsDirectory)
        {
            // Load all files from the selected folder
            LoadFolderFilesRecursively(selectedNode, folderFiles);
            
            // Load content for all files
            foreach (var file in folderFiles.Where(f => !f.IsDirectory))
            {
                await file.LoadContentAsync();
            }
        }
        else
        {
            // If it's a file, just show that file
            folderFiles.Add(selectedNode);
            if (!selectedNode.IsDirectory)
            {
                await selectedNode.LoadContentAsync();
            }
        }
        
        FolderFiles = folderFiles;
        
        // Also update the corresponding files in AllFilesGrouped with the loaded content
        await UpdateAllFilesGroupedWithLoadedContent(folderFiles, selectedNode.AssignmentName);
    }

    private void LoadFolderFilesRecursively(FileTreeNode folderNode, ObservableCollection<FileTreeNode> files)
    {
        try
        {
            if (Directory.Exists(folderNode.FullPath))
            {
                var directory = new DirectoryInfo(folderNode.FullPath);
                
                var directoryFiles = directory.GetFiles().OrderBy(f => f.Name).ToArray();
                var subdirectories = directory.GetDirectories().OrderBy(d => d.Name).ToArray();
                
                // Add files in current directory
                foreach (var file in directoryFiles)
                {
                    var relativePath = Path.GetRelativePath(StudentFolderPath, file.FullName);
                    var assignmentName = GetAssignmentNameFromPath(relativePath);
                    
                    files.Add(new FileTreeNode
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        RelativePath = relativePath,
                        AssignmentName = assignmentName,
                        FileSize = file.Length,
                        LastModified = file.LastWriteTime,
                        IsDirectory = false,
                        FileType = GetFileType(file.Extension),
                        Children = new ObservableCollection<FileTreeNode>()
                    });
                }
                
                // Add subdirectories and their files
                foreach (var subdirectory in subdirectories)
                {
                    var relativePath = Path.GetRelativePath(StudentFolderPath, subdirectory.FullName);
                    var assignmentName = GetAssignmentNameFromPath(relativePath);
                    
                    var dirNode = new FileTreeNode
                    {
                        Name = subdirectory.Name,
                        FullPath = subdirectory.FullName,
                        RelativePath = relativePath,
                        AssignmentName = assignmentName,
                        FileSize = 0,
                        LastModified = subdirectory.LastWriteTime,
                        IsDirectory = true,
                        FileType = "Folder",
                        Children = new ObservableCollection<FileTreeNode>()
                    };
                    
                    files.Add(dirNode);
                    
                    // Recursively add files from subdirectories
                    LoadFolderFilesRecursively(dirNode, files);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error loading files from folder {FolderPath}", folderNode.FullPath);
        }
    }

    /// <summary>
    /// Updates AllFilesGrouped with content from newly loaded FolderFiles
    /// </summary>
    private async Task UpdateAllFilesGroupedWithLoadedContent(ObservableCollection<FileTreeNode> loadedFiles, string assignmentName)
    {
        try
        {
            // Find the matching assignment group in AllFilesGrouped
            var targetGroup = AllFilesGrouped.FirstOrDefault(g => g.AssignmentName == assignmentName);
            if (targetGroup == null)
            {
                Log.Warning("No matching assignment group found in AllFilesGrouped for: {AssignmentName}", assignmentName);
                return;
            }
            
            // Update each file in the target group with loaded content
            foreach (var loadedFile in loadedFiles.Where(f => !f.IsDirectory))
            {
                var targetFile = targetGroup.Files.FirstOrDefault(f => f.FileName == loadedFile.Name);
                if (targetFile != null)
                {
                    // Update on UI thread to ensure proper data binding
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        targetFile.ImageSource = loadedFile.ImageSource;
                        targetFile.CodeContent = loadedFile.CodeContent ?? string.Empty;
                        targetFile.TextContent = loadedFile.TextContent ?? string.Empty;
                        targetFile.IsImage = loadedFile.IsImage;
                        targetFile.IsCode = loadedFile.IsCode;
                        targetFile.IsText = loadedFile.IsText;
                        targetFile.IsBinary = loadedFile.IsBinary;
                        targetFile.IsNone = loadedFile.IsNone;
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating AllFilesGrouped with loaded content for assignment: {AssignmentName}", assignmentName);
        }
    }

    /// <summary>
    /// Builds grouped files by assignment for the main scroll view
    /// </summary>
    private List<AssignmentGroup> BuildGroupedFiles(List<StudentFile> files)
    {
        var grouped = files.GroupBy(f => f.AssignmentName)
                         .Select(g => new AssignmentGroup
                         {
                             AssignmentName = g.Key,
                             Files = g.OrderBy(f => f.FileName).ToList()
                         })
                         .OrderBy(g => g.AssignmentName)
                         .ToList();

        return grouped;
    }

    /// <summary>
    /// Loads content for all files to enable preview functionality
    /// </summary>
    private async Task LoadContentForAllFilesAsync()
    {
        try
        {
            // Load content for all files in all assignment groups
            foreach (var assignmentGroup in AllFilesGrouped)
            {
                foreach (var file in assignmentGroup.Files)
                {
                    // Create a FileTreeNode to load content
                    var fileNode = new FileTreeNode
                    {
                        Name = file.FileName,
                        FullPath = file.FilePath,
                        RelativePath = file.RelativePath,
                        AssignmentName = file.AssignmentName,
                        FileSize = file.FileSize,
                        LastModified = file.LastModified,
                        IsDirectory = false,
                        FileType = GetFileType(Path.GetExtension(file.FilePath))
                    };
                    
                    // Only load content if not already loaded
                    if (!fileNode.IsContentLoaded)
                    {
                        await fileNode.LoadContentAsync();
                    }
                    
                    // Update the StudentFile with the loaded content on the UI thread
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        file.ImageSource = fileNode.ImageSource;
                        file.CodeContent = fileNode.CodeContent;
                        file.TextContent = fileNode.TextContent;
                        file.IsImage = fileNode.IsImage;
                        file.IsCode = fileNode.IsCode;
                        file.IsText = fileNode.IsText;
                        file.IsBinary = fileNode.IsBinary;
                        file.IsNone = fileNode.IsNone;
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading content for all files");
        }
    }
}