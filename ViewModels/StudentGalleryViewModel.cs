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
using SchoolOrganizer.Models;
using SchoolOrganizer.Services;
using SchoolOrganizer.Views.Converters;
using SchoolOrganizer.Views.Windows;

namespace SchoolOrganizer.ViewModels;

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
    
    partial void OnIsAddingStudentChanged(bool value)
    {
        System.Diagnostics.Debug.WriteLine($"IsAddingStudent changed to: {value}");
        System.Diagnostics.Debug.WriteLine($"Stack trace: {System.Environment.StackTrace}");
        // Notify that search enabled state has changed
        OnPropertyChanged(nameof(IsSearchEnabled));
        
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
            var result = IsDoubleClickMode || (!ForceGridView && Students.Count == 2 && Students.Any(s => s is Student) && !string.IsNullOrWhiteSpace(SearchText));
            System.Diagnostics.Debug.WriteLine($"ShowSingleStudent: IsDoubleClickMode={IsDoubleClickMode}, ForceGridView={ForceGridView}, Students.Count={Students.Count}, HasStudent={Students.Any(s => s is Student)}, SearchText='{SearchText}' -> {result}");
            return result;
        }
    }
    
    public bool ShowMultipleStudents 
    { 
        get 
        {
            var result = !IsDoubleClickMode && (ForceGridView || Students.Count != 2 || !Students.Any(s => s is Student) || string.IsNullOrWhiteSpace(SearchText));
            System.Diagnostics.Debug.WriteLine($"ShowMultipleStudents: IsDoubleClickMode={IsDoubleClickMode}, ForceGridView={ForceGridView}, Students.Count={Students.Count}, HasStudent={Students.Any(s => s is Student)}, SearchText='{SearchText}' -> {result}");
            return result;
        }
    }
    
    public bool ShowEmptyState 
    { 
        get 
        {
            var result = Students.Count == 0;
            System.Diagnostics.Debug.WriteLine($"ShowEmptyState: Students.Count={Students.Count} -> {result}");
            return result;
        }
    }
    public Student? FirstStudent => Students.OfType<Student>().FirstOrDefault();
    
    // Search enabled when not adding student
    public bool IsSearchEnabled => !IsAddingStudent;

    // Events
    public event EventHandler? AddStudentRequested;
    public event EventHandler<Student>? StudentImageChangeRequested;
    public event EventHandler<Student>? EditStudentRequested;

    public void UpdateAuthenticationState(GoogleAuthService authService)
    {
        this.authService = authService;
        userProfileService = new UserProfileService(authService);
        IsAuthenticated = true;
        TeacherName = authService.TeacherName;
    }

    public void SetProfileImage(Bitmap? profileImage) => ProfileImage = profileImage;


    public StudentGalleryViewModel(GoogleAuthService? authService = null)
    {
        System.Diagnostics.Debug.WriteLine($"StudentGalleryViewModel constructor - IsAddingStudent initial value: {IsAddingStudent}");
        this.authService = authService;
        if (authService != null)
        {
            userProfileService = new UserProfileService(authService);
            IsAuthenticated = true;
            TeacherName = authService.TeacherName;
            // Don't use Task.Run here to avoid threading issues
            _ = LoadProfileImageAsync();
        }
        else
        {
            IsAuthenticated = false;
            TeacherName = "Not Authenticated";
        }
        System.Diagnostics.Debug.WriteLine($"StudentGalleryViewModel constructor - IsAddingStudent after setup: {IsAddingStudent}");
        System.Diagnostics.Debug.WriteLine($"StudentGalleryViewModel constructor - Initial state: ForceGridView={ForceGridView}, IsLoading={IsLoading}");
        _ = LoadStudents();
    }

    [RelayCommand]
    private async Task LoadStudents()
    {
        IsLoading = true;
        try
        {
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "students.json");
            
            if (File.Exists(jsonPath))
            {
                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                var studentList = JsonSerializer.Deserialize<List<Student>>(jsonContent);
                
                allStudents.Clear();
                Students.Clear();
                if (studentList != null)
                {
                    foreach (var student in studentList)
                    {
                        allStudents.Add(student);
                    }
                }

                // Ensure ForceGridView is true before applying search
                ForceGridView = true;
                IsLoading = false; // Set loading to false before applying search
                await ApplySearchImmediate();
                UpdateDisplayLevelBasedOnItemCount();
                
                // Explicitly update view properties to ensure bindings are refreshed
                UpdateViewProperties();
                
                System.Diagnostics.Debug.WriteLine($"LoadStudents completed - Students.Count: {Students.Count}, ForceGridView: {ForceGridView}, ShowMultipleStudents: {ShowMultipleStudents}, ShowEmptyState: {ShowEmptyState}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading students: {ex.Message}");
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
            
            System.Diagnostics.Debug.WriteLine($"DoubleClickStudent: IsDoubleClickMode={IsDoubleClickMode}, ShowSingleStudent={ShowSingleStudent}, ShowMultipleStudents={ShowMultipleStudents}");
        }
    }

    [RelayCommand]
    private void EditStudent(Student student) => EditStudentRequested?.Invoke(this, student);

    [RelayCommand]
    private void ChangeImage(Student student) => StudentImageChangeRequested?.Invoke(this, student);

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
            // Set ForceGridView BEFORE calling ApplySearchImmediate to ensure view state is correct
            ForceGridView = true;
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
            // Set ForceGridView to ensure we don't show single-student view
            ForceGridView = true;
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
        if (ForceGridView && !string.IsNullOrEmpty(value))
            ForceGridView = false;
        
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

    partial void OnForceGridViewChanged(bool value) => UpdateViewProperties();

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
        System.Diagnostics.Debug.WriteLine($"UpdateViewProperties called - ShowSingleStudent: {ShowSingleStudent}, ShowMultipleStudents: {ShowMultipleStudents}, ShowEmptyState: {ShowEmptyState}");
        OnPropertyChanged(nameof(ShowSingleStudent));
        OnPropertyChanged(nameof(ShowMultipleStudents));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(FirstStudent));
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
        try
        {
            var results = searchService.Search(allStudents, SearchText).ToList();
            
            System.Diagnostics.Debug.WriteLine($"ApplySearchImmediate - SearchText: '{SearchText}', Found {results.Count} students, AllStudents: {allStudents.Count}");
            
            Students.Clear();
            foreach (var s in results)
                Students.Add(s);
            
            // Only add AddStudentCard when NOT in add student mode
            if (!IsAddingStudent)
            {
                Students.Add(new AddStudentCard());
            }
            
            // Defensive check: ensure Students collection is never empty when not in add mode
            if (Students.Count == 0 && !IsAddingStudent)
            {
                Students.Add(new AddStudentCard());
            }
            
            System.Diagnostics.Debug.WriteLine($"ApplySearchImmediate - Final Students.Count: {Students.Count}");
            System.Diagnostics.Debug.WriteLine($"ApplySearchImmediate - ForceGridView: {ForceGridView}");
            System.Diagnostics.Debug.WriteLine($"ApplySearchImmediate - ShowMultipleStudents: {ShowMultipleStudents}");
            System.Diagnostics.Debug.WriteLine($"ApplySearchImmediate - ShowSingleStudent: {ShowSingleStudent}");
            System.Diagnostics.Debug.WriteLine($"ApplySearchImmediate - ShowEmptyState: {ShowEmptyState}");
            System.Diagnostics.Debug.WriteLine($"ApplySearchImmediate - CurrentDisplayLevel: {CurrentDisplayLevel}");
            System.Diagnostics.Debug.WriteLine($"ApplySearchImmediate - IsLoading: {IsLoading}");
            
            // Always update view properties to ensure bindings are refreshed
            UpdateViewProperties();
            UpdateDisplayLevelBasedOnItemCount();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
            // Ensure Students collection is never empty even on error, but only when not in add mode
            if (Students.Count == 0 && !IsAddingStudent)
            {
                Students.Add(new AddStudentCard());
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
        ForceGridView = true;
    }

    [RelayCommand]
    private void CancelAddStudent() 
    {
        System.Diagnostics.Debug.WriteLine("CancelAddStudent command executed - setting IsAddingStudent to false");
        IsAddingStudent = false;
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
        // IMPORTANT: Call this BEFORE setting ForceGridView = true, otherwise it returns early
        UpdateDisplayLevelBasedOnItemCount();
        // Set ForceGridView last to ensure display level is calculated first
        ForceGridView = true;
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
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "students.json");
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
                if (!string.IsNullOrWhiteSpace(oldPath))
                    UniversalImageConverter.ClearCache(oldPath);
                UniversalImageConverter.ClearCache(newPath);
                s.PictureUrl = newPath;
                s.CropSettings = settings;
                s.OriginalImagePath = origPath;
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
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "students.json");
            
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


}
