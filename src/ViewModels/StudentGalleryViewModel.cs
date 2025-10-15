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
        Log.Information("StudentGalleryViewModel constructor started");
        Log.Information("AuthService provided: {HasAuthService}", authService != null);
        Log.Information("Initial state - IsAddingStudent: {IsAddingStudent}, ForceGridView: {ForceGridView}, IsLoading: {IsLoading}", 
            IsAddingStudent, ForceGridView, IsLoading);
        
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
            Log.Debug("No AuthService provided - running in unauthenticated mode");
        }
        
        Log.Information("After setup - IsAddingStudent: {IsAddingStudent}, IsAuthenticated: {IsAuthenticated}, TeacherName: {TeacherName}", 
            IsAddingStudent, IsAuthenticated, TeacherName);
        
        // Subscribe to StudentCoordinatorService events
        SubscribeToCoordinatorEvents();
        Log.Information("Subscribed to StudentCoordinatorService events");
        
        Log.Information("Starting LoadStudents...");
        _ = LoadStudents();
    }

    [RelayCommand]
    private async Task LoadStudents()
    {
        Log.Information("LoadStudents started");
        IsLoading = true;
        Log.Information("Set IsLoading = true");
        
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
                Log.Information("Deserialized {StudentCount} students from JSON", studentList?.Count ?? 0);
                
                allStudents.Clear();
                Students.Clear();
                Log.Information("Cleared existing student collections");
                
                if (studentList != null)
                {
                    foreach (var student in studentList)
                    {
                        allStudents.Add(student);
                        Log.Debug("Added student: {StudentName} (ID: {StudentId})", student.Name, student.Id);
                    }
                    Log.Information("Added {StudentCount} students to AllStudents collection", allStudents.Count);
                }
                else
                {
                    Log.Warning("Deserialized student list is null");
                }

                // Ensure ForceGridView is true before applying search
                ForceGridView = true;
                Log.Information("Set ForceGridView = true");
                
                IsLoading = false; // Set loading to false before applying search
                Log.Information("Set IsLoading = false");
                
                Log.Information("Calling ApplySearchImmediate...");
                await ApplySearchImmediate();
                
                // Batch UI updates to reduce redundant property change notifications
                Dispatcher.UIThread.Post(() =>
                {
                    Log.Information("Calling UpdateDisplayLevelBasedOnItemCount...");
                    UpdateDisplayLevelBasedOnItemCount();
                    
                    Log.Information("Calling UpdateViewProperties...");
                    UpdateViewProperties();
                }, DispatcherPriority.Background);
                
                Log.Information("LoadStudents completed - Students.Count: {StudentsCount}, AllStudents.Count: {AllStudentsCount}, ForceGridView: {ForceGridView}, ShowMultipleStudents: {ShowMultipleStudents}, ShowSingleStudent: {ShowSingleStudent}, ShowEmptyState: {ShowEmptyState}", 
                    Students.Count, allStudents.Count, ForceGridView, ShowMultipleStudents, ShowSingleStudent, ShowEmptyState);
            }
            else
            {
                Log.Debug("students.json file not found at: {JsonPath}", jsonPath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading students from JSON file");
        }
        finally
        {
            IsLoading = false;
            Log.Information("Set IsLoading = false in finally block");
            // Ensure view properties are updated even on error
            UpdateViewProperties();
            Log.Information("Final state - Students.Count: {StudentsCount}, ShowMultipleStudents: {ShowMultipleStudents}, ShowSingleStudent: {ShowSingleStudent}, ShowEmptyState: {ShowEmptyState}", 
                Students.Count, ShowMultipleStudents, ShowSingleStudent, ShowEmptyState);
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
            
            System.Diagnostics.Debug.WriteLine($"DoubleClickStudent: IsDoubleClickMode={IsDoubleClickMode}, ShowSingleStudent={ShowSingleStudent}, ShowMultipleStudents={ShowMultipleStudents}");
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
        UpdateViewProperties();
        UpdateDisplayLevelBasedOnItemCount();
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
        // Batch property change notifications to reduce UI updates
        Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(ShowSingleStudent));
            OnPropertyChanged(nameof(ShowMultipleStudents));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(FirstStudent));
        }, DispatcherPriority.Background);
    }

    private void UpdateDisplayLevelAfterDeletion()
    {
        Dispatcher.UIThread.Post(() => 
        {
            var studentCount = Students?.OfType<Student>().Count() ?? 0;
            var newLevel = cardSizeManager.DetermineSizeByCount(studentCount);
            
            if (newLevel != CurrentDisplayLevel)
            {
                CurrentDisplayLevel = newLevel;
                DisplayConfig = ProfileCardDisplayConfig.GetConfig(newLevel);
                OnPropertyChanged(nameof(DisplayConfig));
            }
        }, DispatcherPriority.Background);
    }

    public void OnWindowResized() => UpdateDisplayLevelBasedOnItemCount();

    public void TriggerCardLayoutUpdate() { }

    private void UpdateDisplayLevelBasedOnItemCount()
    {
        Dispatcher.UIThread.Post(() =>
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
        }, DispatcherPriority.Render);
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
        Log.Information("ApplySearchImmediate started");
        Log.Information("Input - SearchText: '{SearchText}', AllStudents.Count: {AllStudentsCount}, IsAddingStudent: {IsAddingStudent}", 
            SearchText, allStudents.Count, IsAddingStudent);
        
        try
        {
            var results = searchService.Search(allStudents, SearchText).ToList();
            Log.Information("Search completed - Found {ResultCount} students matching '{SearchText}'", results.Count, SearchText);
            
            Students.Clear();
            Log.Information("Cleared Students collection");
            
            foreach (var s in results)
            {
                Students.Add(s);
                Log.Debug("Added student to Students collection: {StudentName} (ID: {StudentId})", s.Name, s.Id);
            }
            
            // Only add AddStudentCard when NOT in add student mode
            if (!IsAddingStudent)
            {
                Students.Add(new AddStudentCard());
                Log.Information("Added AddStudentCard to Students collection");
            }
            else
            {
                Log.Information("Skipped adding AddStudentCard - IsAddingStudent is true");
            }
            
            // Defensive check: ensure Students collection is never empty when not in add mode
            if (Students.Count == 0 && !IsAddingStudent)
            {
                Students.Add(new AddStudentCard());
                Log.Warning("Students collection was empty, added AddStudentCard as fallback");
            }
            
            Log.Information("ApplySearchImmediate completed - Students.Count: {StudentsCount}, ForceGridView: {ForceGridView}, ShowMultipleStudents: {ShowMultipleStudents}, ShowSingleStudent: {ShowSingleStudent}, ShowEmptyState: {ShowEmptyState}, CurrentDisplayLevel: {CurrentDisplayLevel}, IsLoading: {IsLoading}", 
                Students.Count, ForceGridView, ShowMultipleStudents, ShowSingleStudent, ShowEmptyState, CurrentDisplayLevel, IsLoading);
            
            // Always update view properties to ensure bindings are refreshed
            Log.Information("Calling UpdateViewProperties...");
            UpdateViewProperties();
            
            Log.Information("Calling UpdateDisplayLevelBasedOnItemCount...");
            UpdateDisplayLevelBasedOnItemCount();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in ApplySearchImmediate");
            // Ensure Students collection is never empty even on error, but only when not in add mode
            if (Students.Count == 0 && !IsAddingStudent)
            {
                Students.Add(new AddStudentCard());
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
        System.Diagnostics.Debug.WriteLine("AddStudent command executed - setting IsAddingStudent to true");
        IsAddingStudent = true;
    }

    [RelayCommand]
    private void CompleteAddStudent()
    {
        System.Diagnostics.Debug.WriteLine("CompleteAddStudent command executed - setting IsAddingStudent to false");
        IsAddingStudent = false;
    }

    [RelayCommand]
    private void CancelAddStudent() 
    {
        System.Diagnostics.Debug.WriteLine("CancelAddStudent command executed - setting IsAddingStudent to false");
        IsAddingStudent = false;
        StudentBeingEdited = null; // Clear the student being edited
    }

    public void SetStudentForEdit(Student student)
    {
        StudentBeingEdited = student;
        System.Diagnostics.Debug.WriteLine($"SetStudentForEdit called for student: {student.Name} (ID: {student.Id})");
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
                System.Diagnostics.Debug.WriteLine($"Cannot add duplicate student: {name} ({email})");
                return; // Skip adding duplicate student
            }

            var newStudent = new Student
            {
                Id = await GenerateNextStudentIdAsync(),
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
            System.Diagnostics.Debug.WriteLine($"Error adding new student: {ex.Message}");
        }
    }

    public async Task AddMultipleStudentsAsync(List<AddStudentWindow.AddedStudentResult> students)
    {
        try
        {
            var newStudents = new List<Student>();
            var skippedStudents = new List<string>();
            var nextId = await GenerateNextStudentIdAsync();

            foreach (var studentData in students)
            {
                // Check if student already exists (both email AND name must match)
                if (IsStudentDuplicate(studentData.Email, studentData.Name))
                {
                    skippedStudents.Add($"{studentData.Name} ({studentData.Email})");
                    System.Diagnostics.Debug.WriteLine($"Skipping duplicate student: {studentData.Name} ({studentData.Email})");
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
                System.Diagnostics.Debug.WriteLine($"Skipped {skippedStudents.Count} duplicate students: {string.Join(", ", skippedStudents)}");
            }

            await ApplySearchImmediate();
            await SaveAllStudentsToJson();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding multiple students: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Error updating student: {ex.Message}");
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

    private async Task<int> GenerateNextStudentIdAsync()
    {
        var all = await LoadAllStudentsFromJson();
        return all.Any() ? all.Max(s => s.Id) + 1 : 1;
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
            System.Diagnostics.Debug.WriteLine($"Error saving students collection: {ex.Message}");
        }
    }

    public async Task UpdateStudentImage(Student student, string newImagePath, string? cropSettings = null, string? originalImagePath = null)
    {
        try
        {
            await WaitForReadableFileAsync(newImagePath);

            void UpdateStudentPictureUrl(Student s, string oldPath, string newPath, string? settings, string? origPath)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateStudentPictureUrl: Updating student {s.Id} ({s.Name})");
                System.Diagnostics.Debug.WriteLine($"  Old path: {oldPath}");
                System.Diagnostics.Debug.WriteLine($"  New path: {newPath}");
                System.Diagnostics.Debug.WriteLine($"  Crop settings: {settings}");
                System.Diagnostics.Debug.WriteLine($"  Original path: {origPath}");
                
                if (!string.IsNullOrWhiteSpace(oldPath))
                    UniversalImageConverter.ClearCache(oldPath);
                UniversalImageConverter.ClearCache(newPath);
                s.PictureUrl = newPath;
                s.CropSettings = settings;
                s.OriginalImagePath = origPath;
                
                System.Diagnostics.Debug.WriteLine($"  Updated PictureUrl to: {s.PictureUrl}");
            }

            UpdateStudentPictureUrl(student, student.PictureUrl, newImagePath, cropSettings, originalImagePath);

            var studentInCollection = Students.OfType<Student>().FirstOrDefault(s => s.Id == student.Id);
            if (studentInCollection != null && studentInCollection != student)
                UpdateStudentPictureUrl(studentInCollection, studentInCollection.PictureUrl, newImagePath, cropSettings, originalImagePath);

            var inAll = allStudents.FirstOrDefault(s => s.Id == student.Id);
            if (inAll != null && inAll != student)
                UpdateStudentPictureUrl(inAll, inAll.PictureUrl, newImagePath, cropSettings, originalImagePath);

            await SaveAllStudentsToJson();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating student image: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Error loading all students from JSON: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Error loading profile image: {ex.Message}");
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
        coordinator.StudentImageChangeRequested += OnCoordinatorStudentImageChangeRequested;
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

    private async void OnCoordinatorStudentImageChangeRequested(object? sender, Student student)
    {
        await HandleStudentImageChange(student);
    }

    private void OnCoordinatorEditStudentRequested(object? sender, Student student)
    {
        // This will be handled by the view layer
        System.Diagnostics.Debug.WriteLine($"OnCoordinatorEditStudentRequested called for student {student.Id} ({student.Name}) - should be handled by view layer");
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
    /// Handles student image change requests
    /// </summary>
    private Task HandleStudentImageChange(Student student)
    {
        // This method should be handled by the view layer, not the ViewModel
        // The ViewModel should not contain UI logic
        System.Diagnostics.Debug.WriteLine($"HandleStudentImageChange called for student {student.Id} ({student.Name}) - should be handled by view layer");
        return Task.CompletedTask;
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
                System.Diagnostics.Debug.WriteLine($"No assignment folder found for {student.Name}, attempting to download...");
                
                if (string.IsNullOrEmpty(student.Email))
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot download assignments for {student.Name}: Student email not found.");
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
                        System.Diagnostics.Debug.WriteLine($"Download completed but no assignment folder was created for {student.Name}");
                        return;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to download assignments for {student.Name}");
                    return;
                }
            }

            var fileCount = Directory.GetFiles(studentFolderPath, "*", SearchOption.AllDirectories).Length;
            if (fileCount == 0)
            {
                System.Diagnostics.Debug.WriteLine($"No assignment files found for {student.Name}");
                return;
            }

            // Create and show AssignmentViewer
            var detailViewModel = new SchoolOrganizer.Src.ViewModels.StudentDetailViewModel();
            var detailWindow = new SchoolOrganizer.Src.Views.AssignmentManagement.AssignmentViewer(detailViewModel);
            
            // Load the student files asynchronously
            await detailViewModel.LoadStudentFilesAsync(student.Name, student.ClassName, studentFolderPath);
            
            detailWindow.Show();
            System.Diagnostics.Debug.WriteLine($"Opened AssignmentViewer for {student.Name} with {fileCount} files");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening assignments for {student.Name}: {ex.Message}");
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
