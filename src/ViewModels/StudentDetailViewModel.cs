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
using Avalonia.Threading;
using SchoolOrganizer.Src.Services;
using SchoolOrganizer.Src.Models.Assignments;
using SchoolOrganizer.Src.Models.Students;

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
    private ObservableCollection<AssignmentGroup> _allFilesGrouped = new();
    private Student? _student;

    // Navigation properties
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

    public ObservableCollection<AssignmentGroup> AllFilesGrouped
    {
        get => _allFilesGrouped;
        set => this.RaiseAndSetIfChanged(ref _allFilesGrouped, value);
    }

    // Navigation properties
    public bool IsNavigationOpen
    {
        get => _isNavigationOpen;
        set => this.RaiseAndSetIfChanged(ref _isNavigationOpen, value);
    }

    public Student? Student
    {
        get => _student;
        set => this.RaiseAndSetIfChanged(ref _student, value);
    }

    public ReactiveCommand<StudentFile, Unit> OpenFileCommand { get; }

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
    }

    /// <summary>
    /// Loads student files from the specified folder
    /// </summary>
    public async Task LoadStudentFilesAsync(string studentName, string courseName, string studentFolderPath, Student? student = null)
    {
        StudentName = studentName;
        CourseName = courseName;
        StudentFolderPath = studentFolderPath;
        Student = student;
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

            // Build grouped files for the main scroll view
            var groupedFiles = BuildGroupedFiles(files);
            AllFilesGrouped = new ObservableCollection<AssignmentGroup>(groupedFiles);

            // Load ratings and notes from student data
            LoadAssignmentRatings();
            LoadAssignmentNotes();
            LoadAssignmentNotesSidebarWidths();

            // Load content for all files to show images in the assignment gallery
            await LoadContentForAllFilesAsync();
            
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
                // Skip metadata files - they will be loaded with their associated files
                if (file.Name.EndsWith(".gdocmeta.json"))
                    continue;

                var relativePath = Path.GetRelativePath(basePath, file.FullName);
                var assignmentName = GetAssignmentNameFromPath(relativePath);

                var studentFile = new StudentFile
                {
                    FileName = file.Name,
                    FilePath = file.FullName,
                    AssignmentName = assignmentName,
                    FileSize = file.Length,
                    LastModified = file.LastWriteTime,
                    RelativePath = relativePath
                };

                Log.Information("Created StudentFile: FileName={FileName}, FilePath={FilePath}, FileSize={FileSize}, Assignment={Assignment}",
                    file.Name, file.FullName, file.Length, assignmentName);

                Log.Information("Processing file: Name={FileName}, FullPath={FullPath}, Extension={Extension}",
                    file.Name, file.FullName, file.Extension);

                // Check if this file has Google Docs metadata
                var metadataPath = file.FullName + ".gdocmeta.json";
                var metadataExists = File.Exists(metadataPath);

                Log.Information("Checking for metadata: OriginalPath={OriginalPath}, MetadataPath={MetadataPath}, Exists={Exists}",
                    file.FullName, metadataPath, metadataExists);

                // Try to find metadata file even with potential path variations
                if (!metadataExists)
                {
                    // Try alternate paths
                    var fileDirectory = Path.GetDirectoryName(file.FullName);
                    var alternatePath1 = Path.Combine(fileDirectory ?? "", file.Name + ".gdocmeta.json");

                    Log.Information("Trying alternate metadata path: {AlternatePath}", alternatePath1);
                    if (File.Exists(alternatePath1))
                    {
                        metadataPath = alternatePath1;
                        metadataExists = true;
                        Log.Information("Found metadata at alternate path!");
                    }
                }

                if (metadataExists)
                {
                    try
                    {
                        Log.Information("Loading metadata from {MetadataPath}", metadataPath);
                        var metadataJson = await File.ReadAllTextAsync(metadataPath);
                        Log.Information("Metadata JSON loaded, length: {Length}", metadataJson.Length);

                        var metadata = System.Text.Json.JsonSerializer.Deserialize<GoogleDocMetadata>(metadataJson);

                        if (metadata != null)
                        {
                            studentFile.GoogleDocMetadata = metadata;
                            studentFile.IsGoogleDoc = true;
                            studentFile.GoogleDocUrl = metadata.GetEditUrl();
                            studentFile.GoogleDocEmbedUrl = metadata.GetEmbedUrl();

                            Log.Information("✓ Successfully loaded Google Docs metadata for {FileName}: DocType={DocType}, FileId={FileId}, IsGoogleDoc={IsGoogleDoc}",
                                file.Name, metadata.DocType, metadata.FileId, studentFile.IsGoogleDoc);
                        }
                        else
                        {
                            Log.Warning("Metadata deserialization returned null for {MetadataPath}", metadataPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error loading Google Docs metadata from {MetadataPath}", metadataPath);
                    }
                }
                else
                {
                    Log.Information("No Google Docs metadata found for {FileName}", file.Name);
                }

                files.Add(studentFile);
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
    /// Loads assignment ratings from the student's saved data
    /// </summary>
    private void LoadAssignmentRatings()
    {
        if (Student == null || Student.AssignmentRatings == null)
            return;

        foreach (var assignment in AllFilesGrouped)
        {
            if (Student.AssignmentRatings.TryGetValue(assignment.AssignmentName, out int rating))
            {
                assignment.Rating = rating;
            }
        }

        Log.Information("Loaded assignment ratings for student {StudentName}", StudentName);
    }

    /// <summary>
    /// Saves an assignment rating to the student's data and persists to JSON
    /// </summary>
    public async Task SaveAssignmentRatingAsync(string assignmentName, int rating)
    {
        if (Student == null)
        {
            Log.Warning("Cannot save assignment rating - Student object is null");
            return;
        }

        if (string.IsNullOrWhiteSpace(assignmentName))
        {
            Log.Warning("Cannot save assignment rating - assignment name is null or empty");
            return;
        }

        try
        {
            // Initialize AssignmentRatings if null (can happen if deserialized from JSON without this property)
            if (Student.AssignmentRatings == null)
            {
                Student.AssignmentRatings = new Dictionary<string, int>();
            }

            // Update the rating in the student's data
            if (rating > 0)
            {
                Student.AssignmentRatings[assignmentName] = rating;
            }
            else
            {
                // Remove rating if set to 0
                if (Student.AssignmentRatings.ContainsKey(assignmentName))
                {
                    Student.AssignmentRatings.Remove(assignmentName);
                }
            }

            // Save to JSON (delegating to a service would be better, but for now we'll save directly)
            await SaveStudentToJson();

            Log.Information("Saved rating {Rating} for assignment {AssignmentName} for student {StudentName}",
                rating, assignmentName, StudentName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving assignment rating for {AssignmentName} - {ExceptionType}: {Message}", 
                assignmentName, ex.GetType().Name, ex.Message);
            // Don't re-throw to prevent UI crash - error is logged
        }
    }

    /// <summary>
    /// Loads assignment notes from the student's saved data
    /// </summary>
    private void LoadAssignmentNotes()
    {
        if (Student == null || Student.AssignmentNotes == null)
            return;

        foreach (var assignment in AllFilesGrouped)
        {
            if (Student.AssignmentNotes.TryGetValue(assignment.AssignmentName, out string? notes))
            {
                assignment.Notes = notes ?? string.Empty;
            }

            if (Student.AssignmentNotesTimestamps.TryGetValue(assignment.AssignmentName, out DateTime timestamp))
            {
                assignment.LastModified = timestamp;
            }
        }

        Log.Information("Loaded assignment notes for student {StudentName}", StudentName);
    }

    /// <summary>
    /// Loads assignment notes sidebar widths from the student's data
    /// </summary>
    private void LoadAssignmentNotesSidebarWidths()
    {
        if (Student == null || Student.AssignmentNotesSidebarWidths == null)
            return;

        foreach (var assignment in AllFilesGrouped)
        {
            if (Student.AssignmentNotesSidebarWidths.TryGetValue(assignment.AssignmentName, out double width))
            {
                assignment.NotesSidebarWidth = width;
            }
        }

        Log.Information("Loaded assignment notes sidebar widths for student {StudentName}", StudentName);
    }

    /// <summary>
    /// Saves an assignment note to the student's data and persists to JSON
    /// </summary>
    public async Task SaveAssignmentNoteAsync(string assignmentName, string notes)
    {
        if (Student == null)
        {
            Log.Warning("Cannot save assignment note - Student object is null");
            return;
        }

        try
        {
            var now = DateTime.Now;

            // Initialize dictionaries if null (can happen if deserialized from JSON without these properties)
            if (Student.AssignmentNotes == null)
            {
                Student.AssignmentNotes = new Dictionary<string, string>();
            }
            if (Student.AssignmentNotesTimestamps == null)
            {
                Student.AssignmentNotesTimestamps = new Dictionary<string, DateTime>();
            }

            // Update the note and timestamp in the student's data
            if (!string.IsNullOrWhiteSpace(notes))
            {
                Student.AssignmentNotes[assignmentName] = notes;
                Student.AssignmentNotesTimestamps[assignmentName] = now;

                // Update the LastModified in the AssignmentGroup as well
                var assignment = AllFilesGrouped.FirstOrDefault(a => a.AssignmentName == assignmentName);
                if (assignment != null)
                {
                    assignment.LastModified = now;
                }
            }
            else
            {
                // Remove note if empty
                Student.AssignmentNotes.Remove(assignmentName);
                Student.AssignmentNotesTimestamps.Remove(assignmentName);

                // Clear timestamp in AssignmentGroup
                var assignment = AllFilesGrouped.FirstOrDefault(a => a.AssignmentName == assignmentName);
                if (assignment != null)
                {
                    assignment.LastModified = null;
                }
            }

            // Save to JSON
            await SaveStudentToJson();

            Log.Information("Saved note for assignment {AssignmentName} for student {StudentName}",
                assignmentName, StudentName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving assignment note for {AssignmentName}", assignmentName);
        }
    }

    /// <summary>
    /// Saves an assignment notes sidebar width to the student's data and persists to JSON
    /// </summary>
    public async Task SaveAssignmentNotesSidebarWidthAsync(string assignmentName, double width)
    {
        if (Student == null)
        {
            Log.Warning("Cannot save assignment notes sidebar width - Student object is null");
            return;
        }

        try
        {
            // Initialize dictionary if null (can happen if deserialized from JSON without this property)
            if (Student.AssignmentNotesSidebarWidths == null)
            {
                Student.AssignmentNotesSidebarWidths = new Dictionary<string, double>();
            }

            // Update the sidebar width in the student's data
            Student.AssignmentNotesSidebarWidths[assignmentName] = width;

            // Save to JSON
            await SaveStudentToJson();

            Log.Information("Saved notes sidebar width {Width} for assignment {AssignmentName} for student {StudentName}",
                width, assignmentName, StudentName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving assignment notes sidebar width for {AssignmentName}", assignmentName);
        }
    }

    /// <summary>
    /// Saves the student data to JSON file
    /// </summary>
    private async Task SaveStudentToJson()
    {
        if (Student == null)
        {
            Log.Warning("Cannot save student data - Student object is null");
            return;
        }

        await StudentDataLock.FileLock.WaitAsync();
        try
        {
            // Load all students from JSON
            var projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            var jsonPath = Path.Combine(projectRoot, "Data", "students.json");

            if (!File.Exists(jsonPath))
            {
                Log.Warning("students.json file not found at {JsonPath}", jsonPath);
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            var students = System.Text.Json.JsonSerializer.Deserialize<List<Student>>(jsonContent);

            if (students == null)
            {
                Log.Warning("Failed to deserialize students from JSON");
                return;
            }

            // Find and update the current student
            var studentToUpdate = students.FirstOrDefault(s => s.Id == Student.Id);
            if (studentToUpdate != null)
            {
                // Ensure dictionaries are initialized before assigning - use Student's dictionaries if they exist, otherwise create new ones
                studentToUpdate.AssignmentRatings = Student.AssignmentRatings != null 
                    ? new Dictionary<string, int>(Student.AssignmentRatings) 
                    : new Dictionary<string, int>();
                studentToUpdate.AssignmentNotes = Student.AssignmentNotes != null 
                    ? new Dictionary<string, string>(Student.AssignmentNotes) 
                    : new Dictionary<string, string>();
                studentToUpdate.AssignmentNotesTimestamps = Student.AssignmentNotesTimestamps != null 
                    ? new Dictionary<string, DateTime>(Student.AssignmentNotesTimestamps) 
                    : new Dictionary<string, DateTime>();
                studentToUpdate.AssignmentNotesSidebarWidths = Student.AssignmentNotesSidebarWidths != null 
                    ? new Dictionary<string, double>(Student.AssignmentNotesSidebarWidths) 
                    : new Dictionary<string, double>();

                // Save back to JSON
                var updatedJsonContent = System.Text.Json.JsonSerializer.Serialize(students,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(jsonPath, updatedJsonContent);

                Log.Information("Successfully saved student data to JSON");
            }
            else
            {
                Log.Warning("Could not find student with ID {StudentId} in the JSON file", Student.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving student data to JSON - {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
        }
        finally
        {
            StudentDataLock.FileLock.Release();
        }
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
                    // Skip content loading for Google Docs - they have their own viewer
                    if (file.IsGoogleDoc)
                    {
                        Log.Information("Google Doc detected: {FileName}, IsGoogleDoc={IsGoogleDoc}",
                            file.FileName, file.IsGoogleDoc);
                        continue;
                    }

                    // Load content directly into StudentFile
                    await file.LoadContentAsync();

                    Log.Debug("Loaded content for {FileName}: IsImage={IsImage}, IsCode={IsCode}, IsText={IsText}",
                        file.FileName, file.IsImage, file.IsCode, file.IsText);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading content for all files");
        }
    }
}