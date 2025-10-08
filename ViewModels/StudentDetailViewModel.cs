using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Serilog;
using System.Reactive;
using Avalonia.Media.Imaging;
using System.Text;
using Avalonia.Controls;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Threading;
using SchoolOrganizer.Services;
using SchoolOrganizer.Models;

namespace SchoolOrganizer.ViewModels;

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

    public string StudentName
    {
        get => _studentName;
        set => this.RaiseAndSetIfChanged(ref _studentName, value);
    }

    public string CourseName
    {
        get => _courseName;
        set => this.RaiseAndSetIfChanged(ref _courseName, value);
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
            Log.Information("SelectedFile property setter called with: {NodeName}, IsDirectory: {IsDirectory}, AssignmentName: {AssignmentName}, FullPath: {FullPath}", 
                value?.Name ?? "null", value?.IsDirectory ?? false, value?.AssignmentName ?? "null", value?.FullPath ?? "null");
                    
            this.RaiseAndSetIfChanged(ref _selectedFile, value);
            if (value != null)
            {
                Log.Information("Calling LoadFolderFiles for: {NodeName}", value.Name);
                LoadFolderFiles(value);
                
                Log.Information("SelectedFile set complete: Name={Name}, IsDirectory={IsDirectory}, AssignmentName={AssignmentName}", 
                    value.Name, value.IsDirectory, value.AssignmentName);
            }
            else
            {
                Log.Information("SelectedFile set to null");
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


    public ReactiveCommand<StudentFile, Unit> OpenFileCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSelectedFileCommand { get; }

    /// <summary>
    /// Sets the scroll service for this view model
    /// </summary>
    public void SetScrollService(FileViewerScrollService scrollService)
    {
        _scrollService = scrollService;
        Log.Information("Scroll service set in StudentDetailViewModel. Service is null: {IsNull}", scrollService == null);
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
        OpenFileCommand = ReactiveCommand.Create<StudentFile>(OpenFile);
        CloseCommand = ReactiveCommand.Create(() => { });
        OpenSelectedFileCommand = ReactiveCommand.Create(OpenSelectedFile);
    }

    /// <summary>
    /// Loads student files from the specified folder
    /// </summary>
    public async Task LoadStudentFilesAsync(string studentName, string courseName, string studentFolderPath)
    {
        Log.Information("=== LoadStudentFilesAsync started ===");
        Log.Information("Student: {StudentName}, Course: {CourseName}, Path: {FolderPath}", studentName, courseName, studentFolderPath);
        
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

            Log.Information("Directory exists, starting file loading...");
            var files = new List<StudentFile>();
            await LoadFilesRecursively(studentFolderPath, files, studentFolderPath);
            Log.Information("LoadFilesRecursively completed. Found {FileCount} files", files.Count);

            StudentFiles = new ObservableCollection<StudentFile>(files.OrderBy(f => f.AssignmentName).ThenBy(f => f.FileName));
            Log.Information("StudentFiles collection set with {FileCount} files", StudentFiles.Count);
            
            // Build file tree
            Log.Information("Building file tree...");
            var fileTree = BuildFileTree(studentFolderPath, studentFolderPath);
            FileTree = new ObservableCollection<FileTreeNode>(fileTree);
            Log.Information("FileTree built with {TreeNodeCount} nodes", FileTree.Count);
            
            // Build grouped files for the main scroll view
            Log.Information("Building grouped files...");
            var groupedFiles = BuildGroupedFiles(files);
            AllFilesGrouped = new ObservableCollection<AssignmentGroup>(groupedFiles);
            Log.Information("AllFilesGrouped built with {GroupCount} groups", AllFilesGrouped.Count);
            
            // Log group details
            foreach (var group in AllFilesGrouped)
            {
                Log.Information("Group: {GroupName} has {FileCount} files", group.AssignmentName, group.Files.Count);
            }
            
            // Load content for all files to enable preview
            Log.Information("Loading content for all files...");
            await LoadContentForAllFilesAsync();
            Log.Information("Content loading completed");
            
            // Automatically select the first file and load its content
            if (fileTree.Count > 0)
            {
                var firstFile = fileTree.FirstOrDefault();
                if (firstFile != null)
                {
                    Log.Information("Auto-selecting first file: {FirstFileName}, IsDirectory: {IsDirectory}", firstFile.Name, firstFile.IsDirectory);
                    SelectedFile = firstFile;
                    await firstFile.LoadContentAsync();
                    
                    // If it's a directory, load all files in it
                    if (firstFile.IsDirectory)
                    {
                        Log.Information("First file is directory, loading folder files...");
                        LoadFolderFiles(firstFile);
                    }
                }
            }
            else
            {
                Log.Information("No files in tree, creating FolderFiles from all files");
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
                Log.Information("FolderFiles set with {FolderFileCount} files", allFiles.Count);
            }
            
            StatusText = $"Loaded {files.Count} files for {studentName}";
            Log.Information("=== LoadStudentFilesAsync completed successfully ===");
            Log.Information("Final counts - StudentFiles: {StudentFileCount}, FileTree: {TreeCount}, AllFilesGrouped: {GroupCount}, FolderFiles: {FolderFileCount}", 
                StudentFiles.Count, FileTree.Count, AllFilesGrouped.Count, FolderFiles.Count);
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading files: {ex.Message}";
            Log.Error(ex, "=== LoadStudentFilesAsync failed ===");
            Log.Error(ex, "Error loading student files for {StudentName}", studentName);
        }
        finally
        {
            IsLoading = false;
            Log.Information("LoadStudentFilesAsync finally block - IsLoading set to false");
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

    private void OpenFile(StudentFile file)
    {
        if (file == null || string.IsNullOrEmpty(file.FilePath))
        {
            StatusText = "No file selected or file path is empty.";
            Log.Warning("Attempted to open file with null or empty path.");
            return;
        }

        try
        {
            // Use Process.Start to open the file with the default application
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file.FilePath) { UseShellExecute = true });
            StatusText = $"Opened {file.FileName}.";
            Log.Information("Opened file: {FilePath}", file.FilePath);
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
        Log.Information("LoadFolderFiles called for: {NodeName}, IsDirectory: {IsDirectory}", selectedNode.Name, selectedNode.IsDirectory);
        
        var folderFiles = new ObservableCollection<FileTreeNode>();
        
        if (selectedNode.IsDirectory)
        {
            Log.Information("Loading files from directory: {DirectoryPath}", selectedNode.FullPath);
            
            // Load all files from the selected folder
            LoadFolderFilesRecursively(selectedNode, folderFiles);
            Log.Information("Recursive loading complete. Found {Count} items", folderFiles.Count);
            
            // Load content for all files
            var fileCount = folderFiles.Where(f => !f.IsDirectory).Count();
            Log.Information("Loading content for {FileCount} files", fileCount);
            
            foreach (var file in folderFiles.Where(f => !f.IsDirectory))
            {
                Log.Debug("Loading content for file: {FileName}", file.Name);
                await file.LoadContentAsync();
            }
            
            Log.Information("Content loading complete for all files");
        }
        else
        {
            Log.Information("Selected item is a file, adding single file: {FileName}", selectedNode.Name);
            
            // If it's a file, just show that file
            folderFiles.Add(selectedNode);
            if (!selectedNode.IsDirectory)
            {
                Log.Information("Loading content for single file: {FileName}", selectedNode.Name);
                await selectedNode.LoadContentAsync();
            }
        }
        
        Log.Information("Setting FolderFiles collection with {Count} items", folderFiles.Count);
        FolderFiles = folderFiles;
        Log.Information("LoadFolderFiles completed. Loaded {Count} files for folder {FolderName}", folderFiles.Count, selectedNode.Name);
        
        // Also update the corresponding files in AllFilesGrouped with the loaded content
        await UpdateAllFilesGroupedWithLoadedContent(folderFiles, selectedNode.AssignmentName);
        Log.Information("Updated AllFilesGrouped with content from {Count} loaded files", folderFiles.Count);
    }

    private void LoadFolderFilesRecursively(FileTreeNode folderNode, ObservableCollection<FileTreeNode> files)
    {
        Log.Information("LoadFolderFilesRecursively called for: {FolderPath}", folderNode.FullPath);
        
        try
        {
            if (Directory.Exists(folderNode.FullPath))
            {
                Log.Information("Directory exists: {DirectoryPath}", folderNode.FullPath);
                var directory = new DirectoryInfo(folderNode.FullPath);
                
                var directoryFiles = directory.GetFiles().OrderBy(f => f.Name).ToArray();
                var subdirectories = directory.GetDirectories().OrderBy(d => d.Name).ToArray();
                
                Log.Information("Found {FileCount} files and {DirCount} subdirectories in {Directory}", 
                    directoryFiles.Length, subdirectories.Length, folderNode.FullPath);
                
                // Add files in current directory
                foreach (var file in directoryFiles)
                {
                    var relativePath = Path.GetRelativePath(StudentFolderPath, file.FullName);
                    var assignmentName = GetAssignmentNameFromPath(relativePath);
                    
                    Log.Debug("Adding file: {FileName}, RelativePath: {RelativePath}, Assignment: {Assignment}", 
                        file.Name, relativePath, assignmentName);
                    
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
                    
                    Log.Debug("Adding subdirectory: {DirName}, RelativePath: {RelativePath}, Assignment: {Assignment}", 
                        subdirectory.Name, relativePath, assignmentName);
                    
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
                    Log.Debug("Recursively loading subdirectory: {SubDirPath}", subdirectory.FullName);
                    LoadFolderFilesRecursively(dirNode, files);
                }
                
                Log.Information("LoadFolderFilesRecursively completed for: {FolderPath}. Total items: {TotalCount}", 
                    folderNode.FullPath, files.Count);
            }
            else
            {
                Log.Warning("Directory does not exist: {DirectoryPath}", folderNode.FullPath);
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
            Log.Information("UpdateAllFilesGroupedWithLoadedContent called for assignment: {AssignmentName} with {FileCount} files", 
                assignmentName, loadedFiles.Count);
            
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
                    Log.Information("Updating {FileName} - LoadedFile: IsCode={LoadedIsCode}, CodeLength={LoadedCodeLength}, TargetFile: IsCode={TargetIsCode}, CodeLength={TargetCodeLength}", 
                        loadedFile.Name, loadedFile.IsCode, loadedFile.CodeContent?.Length ?? 0, targetFile.IsCode, targetFile.CodeContent?.Length ?? 0);
                    
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
                        
                        Log.Information("Updated AllFilesGrouped file {FileName}: IsCode={IsCode}, CodeContentLength={Length}, FileExtension={Extension}", 
                            targetFile.FileName, targetFile.IsCode, targetFile.CodeContent?.Length ?? 0, targetFile.FileExtension);
                        
                        // Log if this is a code file to verify syntax highlighting data
                        if (targetFile.IsCode)
                        {
                            Log.Information("CODE FILE UPDATED: {FileName} with {CodeLength} characters, FileExtension='{Extension}'", 
                                targetFile.FileName, targetFile.CodeContent?.Length ?? 0, targetFile.FileExtension);
                        }
                    });
                }
                else
                {
                    Log.Warning("File {FileName} not found in AllFilesGrouped for assignment {AssignmentName}", 
                        loadedFile.Name, assignmentName);
                }
            }
            
            Log.Information("AllFilesGrouped update completed for assignment: {AssignmentName}", assignmentName);
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
            Log.Information("Loading content for all files to enable preview");
            
            int totalFiles = AllFilesGrouped.Sum(g => g.Files.Count);
            int processedFiles = 0;
            
            // Load content for all files in all assignment groups
            foreach (var assignmentGroup in AllFilesGrouped)
            {
                Log.Information("Loading content for assignment group: {GroupName} ({FileCount} files)", 
                    assignmentGroup.AssignmentName, assignmentGroup.Files.Count);
                
                foreach (var file in assignmentGroup.Files)
                {
                    processedFiles++;
                    Log.Debug("Loading content for file {Current}/{Total}: {FileName}", 
                        processedFiles, totalFiles, file.FileName);
                    
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
                    
                    await fileNode.LoadContentAsync();
                    
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
                        
                        Log.Information("AllFilesGrouped - Content updated for {FileName}: IsImage={IsImage}, IsCode={IsCode}, IsText={IsText}, IsBinary={IsBinary}, CodeContentLength={CodeLength}, FileExtension={Extension}", 
                            file.FileName, file.IsImage, file.IsCode, file.IsText, file.IsBinary, file.CodeContent?.Length ?? 0, file.FileExtension);
                        
                        // Also verify the file object is the same one in AllFilesGrouped
                        var allFilesFlat = AllFilesGrouped.SelectMany(g => g.Files).ToList();
                        var matchingFile = allFilesFlat.FirstOrDefault(f => f.FileName == file.FileName);
                        if (matchingFile != null)
                        {
                            Log.Information("AllFilesGrouped - Found matching file in collection: IsCode={IsCode}, CodeContentLength={Length}", 
                                matchingFile.IsCode, matchingFile.CodeContent?.Length ?? 0);
                        }
                        else
                        {
                            Log.Warning("AllFilesGrouped - File {FileName} not found in AllFilesGrouped collection!", file.FileName);
                        }
                    });
                }
            }
            
            Log.Information("Successfully loaded content for all {TotalFiles} files", totalFiles);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading content for all files");
        }
    }
}