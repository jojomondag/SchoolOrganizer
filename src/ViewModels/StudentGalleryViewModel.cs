using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolOrganizer.Src.Models.Students;
using SchoolOrganizer.Src.Models.UI;
using SchoolOrganizer.Src.Services;
using SchoolOrganizer.Src.Converters;
using SchoolOrganizer.Src.Views.Windows;
using Avalonia.Controls;
using SchoolOrganizer.Src.Views.Windows.ImageCrop;
using Serilog;

namespace SchoolOrganizer.Src.ViewModels;

public partial class StudentGalleryViewModel : ObservableObject
{
    private readonly StudentSearchService searchService = new();
    private readonly CardSizeManager cardSizeManager = new();
    private GoogleAuthService? authService;
    private UserProfileService? userProfileService;
    private ObservableCollection<Student> allStudents = new();
    private System.Threading.CancellationTokenSource? searchCts;

    public ObservableCollection<Student> AllStudents => allStudents;
    public GoogleAuthService? AuthService => authService;

    [ObservableProperty]
    private ObservableCollection<IPerson> students = new();

    [ObservableProperty]
    private Student? selectedStudent;

    // Computed properties for safe binding
    public string SelectedStudentName => SelectedStudent?.Name ?? "No student selected";
    public string SelectedStudentEmail => SelectedStudent?.Email ?? "No student selected";
    public string SelectedStudentClassName => SelectedStudent?.ClassName ?? "No student selected";
    public string SelectedStudentSecondaryInfo => SelectedStudent?.SecondaryInfo ?? "No student selected";

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private ProfileCardDisplayLevel currentDisplayLevel = ProfileCardDisplayLevel.Medium;

    [ObservableProperty]
    private ProfileCardDisplayConfig displayConfig = ProfileCardDisplayConfig.GetConfig(ProfileCardDisplayLevel.Medium);

    [ObservableProperty]
    private bool forceGridView = true;

    [ObservableProperty]
    private Bitmap? profileImage;

    [ObservableProperty]
    private string teacherName = "Unknown Teacher";

    [ObservableProperty]
    private bool isAuthenticated = false;

    [ObservableProperty]
    private bool isDoubleClickMode = false;

    [ObservableProperty]
    private bool isDownloadingAssignments = false;

    [ObservableProperty]
    private string downloadStatusText = string.Empty;

    [ObservableProperty]
    private bool isAddingStudent = false;

    [ObservableProperty]
    private Student? studentBeingEdited = null;
    
    partial void OnIsAddingStudentChanged(bool value)
    {
        
        // Refresh the Students collection to show/hide the Add Student Card
        _ = ApplySearchImmediate();
        
        // Force UI update for visibility bindings
        OnPropertyChanged(nameof(IsAddingStudent));
    }

    // Properties for controlling view mode
    public bool ShowSingleStudent 
    { 
        get 
        {
            // Show single student large card ONLY when:
            // 1. User double-clicked a student (IsDoubleClickMode = true)
            // Note: Removed automatic single card view - always use grid view for consistency
            var result = IsDoubleClickMode;
            return result;
        }
    }
    
    public bool ShowMultipleStudents 
    { 
        get 
        {
            // Show grid view when NOT showing single student
            var result = !ShowSingleStudent;
            return result;
        }
    }
    
    public bool ShowEmptyState 
    { 
        get 
        {
            var result = Students.Count == 0;
            return result;
        }
    }
    public Student? FirstStudent => Students.OfType<Student>().FirstOrDefault();
    

    // Events - Removed direct event publishers, now using StudentCoordinatorService

    public void UpdateAuthenticationState(GoogleAuthService authService)
    {
        this.authService = authService;
        userProfileService = new UserProfileService(authService);
        IsAuthenticated = true;
        TeacherName = authService.TeacherName;
        
        // Notify property change to trigger AddStudentView auth service update
        OnPropertyChanged(nameof(IsAuthenticated));
    }

    public void SetProfileImage(Bitmap? profileImage) => ProfileImage = profileImage;


    public StudentGalleryViewModel(GoogleAuthService? authService = null)
    {
        this.authService = authService;
        if (authService != null)
        {
            userProfileService = new UserProfileService(authService);
            IsAuthenticated = true;
            TeacherName = authService.TeacherName;
            Log.Information("Authentication setup - TeacherName: {TeacherName}", TeacherName);
            // Don't use Task.Run here to avoid threading issues
            _ = LoadProfileImageAsync();
        }
        else
        {
            IsAuthenticated = false;
            TeacherName = "Not Authenticated";
        }
        
        // Subscribe to StudentCoordinatorService events
        SubscribeToCoordinatorEvents();
        
        _ = LoadStudents();
    }

    [RelayCommand]
    private async Task LoadStudents()
    {
        IsLoading = true;
        
        try
        {
            var projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            var jsonPath = Path.Combine(projectRoot, "Data", "students.json");
            Log.Information("Looking for students.json at: {JsonPath}", jsonPath);
            
            if (File.Exists(jsonPath))
            {
                Log.Information("students.json file found, reading content...");
                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                Log.Information("JSON content length: {ContentLength} characters", jsonContent.Length);
                
                var studentList = JsonSerializer.Deserialize<List<Student>>(jsonContent);
                
                allStudents.Clear();
                Students.Clear();
                
                if (studentList != null)
                {
                    // Fix duplicate IDs if they exist
                    FixDuplicateStudentIds(studentList);
                    
                    foreach (var student in studentList)
                    {
                        allStudents.Add(student);
                    }
                }
                else
                {
                    Log.Warning("Deserialized student list is null");
                }

                // Ensure ForceGridView is true before applying search
                ForceGridView = true;
                
                IsLoading = false; // Set loading to false before applying search
                
                await ApplySearchImmediate();
                
                // Update display level and view properties directly
                UpdateDisplayLevelBasedOnItemCount();
                UpdateViewProperties();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading students from JSON file");
        }
        finally
        {
            IsLoading = false;
            // Ensure view properties are updated even on error
            UpdateViewProperties();
        }
    }

    [RelayCommand]
    private void SelectStudent(IPerson? person)
    {
        if (person is AddStudentCard)
        {
            AddStudent();
            return;
        }
        
        if (person is Student student)
        {
            SelectedStudent = student;
            ForceGridView = false;
        }
    }

    [RelayCommand]
    private void DoubleClickStudent(IPerson? person)
    {
        if (person is Student student)
        {
            SelectedStudent = student;
            ForceGridView = false;
            IsDoubleClickMode = true;
            
            // Filter Students collection to show only the double-clicked student + AddStudentCard
            var filteredStudents = new ObservableCollection<IPerson> { student, new AddStudentCard() };
            Students = filteredStudents;
            
            // Update view properties to ensure UI reflects the change
            UpdateViewProperties();
            
        }
    }

    [RelayCommand]
    private void EditStudent(Student student) => Services.StudentCoordinatorService.Instance.PublishEditStudentRequested(student);

    [RelayCommand]
    private void ChangeImage(Student student) => Services.StudentCoordinatorService.Instance.PublishStudentImageChangeRequested(student);

    [RelayCommand]
    private async Task DeselectStudent() 
    {
        SelectedStudent = null;
        
        // Exit double-click mode when deselecting
        if (IsDoubleClickMode)
        {
            IsDoubleClickMode = false;
            // Cancel any pending debounced searches
            searchCts?.Cancel();
            // Reset search text
            SearchText = string.Empty;
            // Immediately restore the full student list (not debounced)
            await ApplySearchImmediate();
            // Let automatic sizing determine the optimal display level based on student count
            UpdateDisplayLevelBasedOnItemCount();
            // Explicitly notify all view properties to ensure UI updates
            UpdateViewProperties();
            // Explicitly notify that Students collection changed
            OnPropertyChanged(nameof(Students));
        }
        else
        {
            // For single-click deselection, only clear selection without refreshing cards
            // Cancel any pending debounced searches
            searchCts?.Cancel();
            // Update view properties to update visibility bindings
            UpdateViewProperties();
        }
    }

    [RelayCommand]
    private void DeleteStudent(Student? student)
    {
        if (student == null) return;

        try
        {
            if (SelectedStudent?.Id == student.Id)
                SelectedStudent = null;

            allStudents.Remove(student);
            var studentInView = Students.OfType<Student>().FirstOrDefault(s => s.Id == student.Id);
            if (studentInView != null)
                Students.Remove(studentInView);

            UpdateViewProperties();
            UpdateDisplayLevelAfterDeletion();
            _ = Task.Run(SaveAllStudentsToJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting student: {ex.Message}");
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Exit double-click mode when search text changes
        if (IsDoubleClickMode)
            IsDoubleClickMode = false;
            
        _ = ApplySearchDebounced();
    }

    partial void OnStudentsChanged(ObservableCollection<IPerson> value)
    {
        if (SelectedStudent != null && !Students.Contains(SelectedStudent))
            SelectedStudent = null;
        
        // Batch property updates to reduce UI refresh cycles
        UpdateViewPropertiesAndDisplayLevel();
    }


    partial void OnSelectedStudentChanged(Student? value)
    {
        // Notify computed properties that they may have changed
        OnPropertyChanged(nameof(SelectedStudentName));
        OnPropertyChanged(nameof(SelectedStudentEmail));
        OnPropertyChanged(nameof(SelectedStudentClassName));
        OnPropertyChanged(nameof(SelectedStudentSecondaryInfo));
    }

    private void UpdateViewProperties()
    {
        // Direct property change notifications for better performance
        OnPropertyChanged(nameof(ShowSingleStudent));
        OnPropertyChanged(nameof(ShowMultipleStudents));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(FirstStudent));
    }

    private void UpdateViewPropertiesAndDisplayLevel()
    {
        // Batch both view properties and display level updates to minimize UI refresh cycles
        var studentCount = Students?.OfType<Student>().Count() ?? 0;
        var newLevel = cardSizeManager.DetermineSizeByCount(studentCount);
        
        // Update display level if needed
        if (newLevel != CurrentDisplayLevel)
        {
            CurrentDisplayLevel = newLevel;
            DisplayConfig = ProfileCardDisplayConfig.GetConfig(newLevel);
            OnPropertyChanged(nameof(DisplayConfig));
            OnPropertyChanged(nameof(CurrentDisplayLevel));
        }
        
        // Update view properties
        OnPropertyChanged(nameof(ShowSingleStudent));
        OnPropertyChanged(nameof(ShowMultipleStudents));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(FirstStudent));
    }

    private void UpdateDisplayLevelAfterDeletion()
    {
        var studentCount = Students?.OfType<Student>().Count() ?? 0;
        var newLevel = cardSizeManager.DetermineSizeByCount(studentCount);
        
        if (newLevel != CurrentDisplayLevel)
        {
            CurrentDisplayLevel = newLevel;
            DisplayConfig = ProfileCardDisplayConfig.GetConfig(newLevel);
            OnPropertyChanged(nameof(DisplayConfig));
        }
    }

    public void OnWindowResized() => UpdateDisplayLevelBasedOnItemCount();

    public void TriggerCardLayoutUpdate() { }

    private void UpdateDisplayLevelBasedOnItemCount()
    {
        var studentCount = Students?.OfType<Student>().Count() ?? 0;
        var newLevel = CalculateOptimalDisplayLevel(studentCount);
        
        if (newLevel != CurrentDisplayLevel)
        {
            CurrentDisplayLevel = newLevel;
            DisplayConfig = ProfileCardDisplayConfig.GetConfig(newLevel);
            OnPropertyChanged(nameof(DisplayConfig));
            OnPropertyChanged(nameof(CurrentDisplayLevel));
        }
    }

    private ProfileCardDisplayLevel CalculateOptimalDisplayLevel(int studentCount)
    {
        var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
            ? desktop.MainWindow 
            : null;
        
        var windowWidth = mainWindow?.Width ?? 1200;
        return cardSizeManager.DetermineOptimalSize(studentCount, windowWidth);
    }


    private Task ApplySearchImmediate()
    {
        try
        {
            var results = searchService.Search(allStudents, SearchText).ToList();
            
            // Create new collection to avoid individual UI updates
            var newStudents = new ObservableCollection<IPerson>();
            
            foreach (var s in results)
            {
                newStudents.Add(s);
            }
            
            // Only add AddStudentCard when NOT in add student mode
            if (!IsAddingStudent)
            {
                newStudents.Add(new AddStudentCard());
            }
            
            // Defensive check: ensure Students collection is never empty when not in add mode
            if (newStudents.Count == 0 && !IsAddingStudent)
            {
                newStudents.Add(new AddStudentCard());
                Log.Warning("Students collection was empty, added AddStudentCard as fallback");
            }
            
            // Replace entire collection in one operation to minimize UI updates
            Students = newStudents;
            
            // Update view properties and display level in one batch
            UpdateViewPropertiesAndDisplayLevel();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in ApplySearchImmediate");
            // Ensure Students collection is never empty even on error, but only when not in add mode
            if (Students.Count == 0 && !IsAddingStudent)
            {
                Students = new ObservableCollection<IPerson> { new AddStudentCard() };
                Log.Warning("Added AddStudentCard as error fallback");
            }
            // Update view properties even on error to ensure UI state is correct
            UpdateViewProperties();
        }
        return Task.CompletedTask;
    }

    private async Task ApplySearchDebounced(int delayMs = 250)
    {
        searchCts?.Cancel();
        var cts = new System.Threading.CancellationTokenSource();
        searchCts = cts;
        try
        {
            await Task.Delay(delayMs, cts.Token);
            if (cts.IsCancellationRequested) return;
            await ApplySearchImmediate();
        }
        catch (TaskCanceledException) { }
    }


    [RelayCommand]
    private void AddStudent() 
    {
        IsAddingStudent = true;
    }

    [RelayCommand]
    private void CompleteAddStudent()
    {
        IsAddingStudent = false;
    }

    [RelayCommand]
    private void CancelAddStudent() 
    {
        IsAddingStudent = false;
        StudentBeingEdited = null; // Clear the student being edited
    }

    public void SetStudentForEdit(Student student)
    {
        StudentBeingEdited = student;
    }

    [RelayCommand]
    private async Task BackToGallery()
    {
        SearchText = string.Empty;
        SelectedStudent = null;
        IsDoubleClickMode = false;
        // Immediately restore the full student list
        await ApplySearchImmediate();
        // Let automatic sizing determine the optimal display level based on student count
        UpdateDisplayLevelBasedOnItemCount();
    }

    [RelayCommand]
    private void SetDisplayLevel(ProfileCardDisplayLevel level)
    {
        CurrentDisplayLevel = level;
        DisplayConfig = ProfileCardDisplayConfig.GetConfig(level);
    }

    public async Task AddNewStudentAsync(string name, string className, List<string> teachers, string email, DateTime enrollmentDate, string picturePath)
    {
        try
        {
            // Check if student already exists (both email AND name must match)
            if (IsStudentDuplicate(email, name))
            {
                return; // Skip adding duplicate student
            }

            var newStudent = new Student
            {
                Id = GenerateNextStudentId(),
                Name = name,
                ClassName = className,
                Email = email,
                EnrollmentDate = enrollmentDate,
                PictureUrl = picturePath ?? string.Empty
            };

            foreach (var teacher in teachers ?? new List<string>())
                newStudent.AddTeacher(teacher);

            allStudents.Add(newStudent);
            await ApplySearchImmediate();
            await SaveAllStudentsToJson();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error adding new student");
        }
    }

    public async Task AddMultipleStudentsAsync(List<AddStudentWindow.AddedStudentResult> students)
    {
        try
        {
            var newStudents = new List<Student>();
            var skippedStudents = new List<string>();
            var nextId = GenerateNextStudentId();

            foreach (var studentData in students)
            {
                // Check if student already exists (both email AND name must match)
                if (IsStudentDuplicate(studentData.Email, studentData.Name))
                {
                    skippedStudents.Add($"{studentData.Name} ({studentData.Email})");
                    continue;
                }

                var newStudent = new Student
                {
                    Id = nextId++,
                    Name = studentData.Name,
                    ClassName = studentData.ClassName,
                    Email = studentData.Email,
                    EnrollmentDate = studentData.EnrollmentDate,
                    PictureUrl = studentData.PicturePath ?? string.Empty
                };

                foreach (var teacher in studentData.Teachers ?? new List<string>())
                    newStudent.AddTeacher(teacher);

                newStudents.Add(newStudent);
            }

            foreach (var student in newStudents)
            {
                allStudents.Add(student);
            }

            if (skippedStudents.Count > 0)
            {
                Log.Information($"Skipped {skippedStudents.Count} duplicate students: {string.Join(", ", skippedStudents)}");
            }

            await ApplySearchImmediate();
            await SaveAllStudentsToJson();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error adding multiple students");
        }
    }

    public async Task UpdateExistingStudentAsync(Student student, string name, string className, List<string> teachers, string email, DateTime enrollmentDate, string picturePath)
    {
        try
        {
            UpdateStudentProperties(student, name, className, teachers, email, enrollmentDate, picturePath);

            var inAll = allStudents.FirstOrDefault(s => s.Id == student.Id);
            if (inAll != null && inAll != student)
                UpdateStudentProperties(inAll, name, className, teachers, email, enrollmentDate, picturePath);

            await ApplySearchImmediate();
            await SaveAllStudentsToJson();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating student");
        }
    }

    private static void UpdateStudentProperties(Student student, string name, string className, List<string> teachers, string email, DateTime enrollmentDate, string picturePath)
    {
        student.Name = name;
        student.ClassName = className;
        student.Email = email;
        student.EnrollmentDate = enrollmentDate;
        student.PictureUrl = picturePath ?? string.Empty;
        
        student.ClearTeachers();
        foreach (var teacher in teachers ?? new List<string>())
            student.AddTeacher(teacher);
    }

    private int GenerateNextStudentId()
    {
        // Use the current in-memory collection instead of loading from JSON
        // This ensures we get the correct next ID even when multiple students are added quickly
        return allStudents.Any() ? allStudents.Max(s => s.Id) + 1 : 1;
    }

    /// <summary>
    /// Fixes duplicate student IDs by assigning unique IDs to students with duplicate IDs
    /// </summary>
    private void FixDuplicateStudentIds(List<Student> students)
    {
        var idGroups = students.GroupBy(s => s.Id).Where(g => g.Count() > 1).ToList();
        
        if (idGroups.Any())
        {
            Log.Warning("Found {Count} groups of students with duplicate IDs, fixing...", idGroups.Count);
            
            var nextId = students.Max(s => s.Id) + 1;
            
            foreach (var group in idGroups)
            {
                // Keep the first student with the original ID, assign new IDs to the rest
                var studentsWithDuplicateId = group.ToList();
                for (int i = 1; i < studentsWithDuplicateId.Count; i++)
                {
                    studentsWithDuplicateId[i].Id = nextId++;
                    Log.Information("Fixed duplicate ID for student: {Name} (assigned ID: {NewId})", 
                        studentsWithDuplicateId[i].Name, studentsWithDuplicateId[i].Id);
                }
            }
            
            Log.Information("Fixed duplicate IDs. Next available ID: {NextId}", nextId);
        }
    }

    private async Task SaveAllStudentsToJson()
    {
        try
        {
            var projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            var jsonPath = Path.Combine(projectRoot, "Data", "students.json");
            var jsonContent = JsonSerializer.Serialize(allStudents.ToList(), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(jsonPath, jsonContent);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving students collection");
        }
    }

    public async Task UpdateStudentImage(Student student, string newImagePath, string? cropSettings = null, string? originalImagePath = null)
    {
        try
        {
            await WaitForReadableFileAsync(newImagePath);

            void UpdateStudentPictureUrl(Student s, string oldPath, string newPath, string? settings, string? origPath)
            {
                // Clear cache for both old and new paths to ensure fresh loading
                if (!string.IsNullOrWhiteSpace(oldPath))
                    UniversalImageConverter.ClearCache(oldPath);
                UniversalImageConverter.ClearCache(newPath);
                
                // Update properties in the correct order to ensure proper change notifications
                s.CropSettings = settings;
                s.OriginalImagePath = origPath;
                
                // Force property change notification by temporarily clearing and setting the path
                // This ensures the binding system detects the change
                s.PictureUrl = "";
                s.PictureUrl = newPath;
            }

            // Find all student instances by ID and update them consistently
            var studentId = student.Id;
            
            // Update the main student object (if it's the correct one by ID)
            if (student.Id == studentId)
            {
                UpdateStudentPictureUrl(student, student.PictureUrl, newImagePath, cropSettings, originalImagePath);
            }

            // Update the student in the Students collection (the one displayed in UI)
            var studentInCollection = Students.OfType<Student>().FirstOrDefault(s => s.Id == studentId);
            if (studentInCollection != null)
            {
                UpdateStudentPictureUrl(studentInCollection, studentInCollection.PictureUrl, newImagePath, cropSettings, originalImagePath);
            }

            // Update the student in the allStudents collection
            var inAll = allStudents.FirstOrDefault(s => s.Id == studentId);
            if (inAll != null)
            {
                UpdateStudentPictureUrl(inAll, inAll.PictureUrl, newImagePath, cropSettings, originalImagePath);
            }

            // Force UI refresh by notifying property changes on the UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Trigger a refresh of the Students collection to ensure UI updates
                OnPropertyChanged(nameof(Students));
            });

            await SaveAllStudentsToJson();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating student image");
        }
    }

    private static async Task<bool> WaitForReadableFileAsync(string path, int maxAttempts = 20, int delayMs = 100)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length > 0) return true;
            }
            catch { }
            await Task.Delay(delayMs);
        }
        return false;
    }

    private async Task<List<Student>> LoadAllStudentsFromJson()
    {
        try
        {
            var projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            var jsonPath = Path.Combine(projectRoot, "Data", "students.json");
            
            if (File.Exists(jsonPath))
            {
                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                var studentList = JsonSerializer.Deserialize<List<Student>>(jsonContent);
                return studentList ?? new List<Student>();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading all students from JSON");
        }
        
        return new List<Student>();
    }

    private async Task LoadProfileImageAsync()
    {
        try
        {
            if (userProfileService != null)
            {
                var (profileImage, _) = await userProfileService.LoadProfileImageAsync();
                await Dispatcher.UIThread.InvokeAsync(() => ProfileImage = profileImage);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading profile image");
            await Dispatcher.UIThread.InvokeAsync(() => ProfileImage = null);
        }
    }

    /// <summary>
    /// Checks if a student already exists by comparing both email AND name (case-insensitive)
    /// </summary>
    private bool IsStudentDuplicate(string email, string name)
    {
        return allStudents.Any(s => 
            s.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && 
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Subscribes to StudentCoordinatorService events
    /// </summary>
    private void SubscribeToCoordinatorEvents()
    {
        var coordinator = Services.StudentCoordinatorService.Instance;
        
        coordinator.StudentSelected += OnCoordinatorStudentSelected;
        coordinator.StudentDeselected += OnCoordinatorStudentDeselected;
        coordinator.AddStudentRequested += OnCoordinatorAddStudentRequested;
        coordinator.StudentAdded += OnCoordinatorStudentAdded;
        coordinator.StudentUpdated += OnCoordinatorStudentUpdated;
        coordinator.EditStudentRequested += OnCoordinatorEditStudentRequested;
        coordinator.ViewAssignmentsRequested += OnCoordinatorViewAssignmentsRequested;
        coordinator.ManualEntryRequested += OnCoordinatorManualEntryRequested;
        coordinator.ClassroomImportRequested += OnCoordinatorClassroomImportRequested;
        coordinator.AddStudentCompleted += OnCoordinatorAddStudentCompleted;
        coordinator.AddStudentCancelled += OnCoordinatorAddStudentCancelled;
        coordinator.StudentImageUpdated += OnCoordinatorStudentImageUpdated;
    }

    private void OnCoordinatorStudentSelected(object? sender, Student student)
    {
        SelectStudentCommand.Execute(student);
    }

    private void OnCoordinatorStudentDeselected(object? sender, EventArgs e)
    {
        DeselectStudentCommand.Execute(null);
    }

    private void OnCoordinatorAddStudentRequested(object? sender, EventArgs e)
    {
        AddStudentCommand.Execute(null);
    }

    private async void OnCoordinatorStudentAdded(object? sender, Student student)
    {
        // Add the student to the collection
        await AddNewStudentAsync(
            student.Name,
            student.ClassName,
            student.Teachers.ToList(),
            student.Email,
            student.EnrollmentDate,
            student.PictureUrl
        );
    }

    private async void OnCoordinatorStudentUpdated(object? sender, Student student)
    {
        // Update the existing student in the collection
        await UpdateExistingStudentAsync(
            student,
            student.Name,
            student.ClassName,
            student.Teachers.ToList(),
            student.Email,
            student.EnrollmentDate,
            student.PictureUrl
        );
    }


    private void OnCoordinatorEditStudentRequested(object? sender, Student student)
    {
        // This will be handled by the view layer
    }

    private async void OnCoordinatorViewAssignmentsRequested(object? sender, Student student)
    {
        await HandleViewAssignments(student);
    }

    private void OnCoordinatorManualEntryRequested(object? sender, EventArgs e)
    {
        // This will be handled by the view layer
    }

    private void OnCoordinatorClassroomImportRequested(object? sender, EventArgs e)
    {
        // This will be handled by the view layer
    }

    private void OnCoordinatorAddStudentCompleted(object? sender, EventArgs e)
    {
        CompleteAddStudentCommand.Execute(null);
    }

    private void OnCoordinatorAddStudentCancelled(object? sender, EventArgs e)
    {
        CancelAddStudentCommand.Execute(null);
    }

    private async void OnCoordinatorStudentImageUpdated(object? sender, (Student student, string imagePath, string? cropSettings, string? originalImagePath) args)
    {
        await UpdateStudentImage(args.student, args.imagePath, args.cropSettings, args.originalImagePath);
    }


    /// <summary>
    /// Handles view assignments requests
    /// </summary>
    private async Task HandleViewAssignments(Student student)
    {
        try
        {
            // Find the student's assignment folder by searching through existing course folders
            var studentFolderPath = await FindStudentAssignmentFolder(student);
            
            if (string.IsNullOrEmpty(studentFolderPath))
            {
                // Try to download assignments for this student
                if (string.IsNullOrEmpty(student.Email))
                {
                    return;
                }

                // Set download status
                IsDownloadingAssignments = true;
                DownloadStatusText = "Preparing to download assignments...";

                // Attempt to download assignments for this student
                var assignmentService = new Services.StudentAssignmentService();
                var downloadSuccess = await assignmentService.DownloadStudentAssignmentsAsync(student, authService!, TeacherName);
                
                if (downloadSuccess)
                {
                    // Retry finding the folder after download
                    studentFolderPath = await FindStudentAssignmentFolder(student);
                    
                    if (string.IsNullOrEmpty(studentFolderPath))
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            var fileCount = await Task.Run(() => Directory.GetFiles(studentFolderPath, "*", SearchOption.AllDirectories).Length);
            if (fileCount == 0)
            {
                return;
            }

            // Create and show AssignmentViewer
            var detailViewModel = new SchoolOrganizer.Src.ViewModels.StudentDetailViewModel();
            var detailWindow = new SchoolOrganizer.Src.Views.AssignmentManagement.AssignmentViewer(detailViewModel);
            
            // Load the student files asynchronously
            await detailViewModel.LoadStudentFilesAsync(student.Name, student.ClassName, studentFolderPath);
            
            detailWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error opening assignments for {student.Name}");
        }
        finally
        {
            // Clear download status
            IsDownloadingAssignments = false;
            DownloadStatusText = string.Empty;
        }
    }

    /// <summary>
    /// Find the student's assignment folder by searching through existing course folders
    /// </summary>
    private async Task<string?> FindStudentAssignmentFolder(Student student)
    {
        var assignmentService = new Services.StudentAssignmentService();
        return await assignmentService.FindStudentAssignmentFolderAsync(student);
    }



}
