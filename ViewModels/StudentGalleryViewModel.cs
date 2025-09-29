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

namespace SchoolOrganizer.ViewModels;

public partial class StudentGalleryViewModel : ViewModelBase
{
    private readonly StudentSearchService searchService = new();
    private readonly CardSizeManager cardSizeManager = new();
    private GoogleAuthService? authService;
    private UserProfileService? userProfileService;

    // Full, unfiltered dataset kept in-memory
    private ObservableCollection<Student> allStudents = new();

    public ObservableCollection<Student> AllStudents => allStudents;

    [ObservableProperty]
    private ObservableCollection<IPerson> students = new();

    [ObservableProperty]
    private Student? selectedStudent;

    [ObservableProperty]
    private string searchText = string.Empty;


    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private ProfileCardDisplayLevel currentDisplayLevel = ProfileCardDisplayLevel.Medium;

    [ObservableProperty]
    private ProfileCardDisplayConfig displayConfig = ProfileCardDisplayConfig.GetConfig(ProfileCardDisplayLevel.Medium);

    [ObservableProperty]
    private bool forceGridView = false;

    // Authentication properties
    [ObservableProperty]
    private Bitmap? profileImage;

    [ObservableProperty]
    private string teacherName = "Unknown Teacher";

    [ObservableProperty]
    private bool isAuthenticated = false;

    partial void OnIsAuthenticatedChanged(bool value)
    {
        // Authentication state changed - no need to log every change
    }

    // Properties for controlling view mode
    public bool ShowSingleStudent => !ForceGridView && Students.Count == 2 && Students.Any(s => s is Student); // Only one actual student + add card
    public bool ShowMultipleStudents => ForceGridView || Students.Count != 2 || !Students.Any(s => s is Student); // Multiple students or no students
    public bool ShowEmptyState => Students.Count == 1 && !Students.Any(s => s is Student); // Only add card, no actual students
    
    // Safe property to get first student without index errors
    public Student? FirstStudent => Students.OfType<Student>().FirstOrDefault();

    // Event for requesting image selection
    public event EventHandler? AddStudentRequested;
    
    // Event raised when a student's image should be changed (on card click)
    public event EventHandler<Student>? StudentImageChangeRequested;
    public event EventHandler<Student>? EditStudentRequested;

    // Method to update authentication state from MainWindowViewModel
    public void UpdateAuthenticationState(GoogleAuthService authService)
    {
        this.authService = authService;
        userProfileService = new UserProfileService(authService);
        IsAuthenticated = true;
        TeacherName = authService.TeacherName;
        // Profile image loading is handled by MainWindowViewModel
    }

    // Method to set profile image from MainWindowViewModel
    public void SetProfileImage(Bitmap? profileImage)
    {
        ProfileImage = profileImage;
    }


    public StudentGalleryViewModel(GoogleAuthService? authService = null)
    {
        this.authService = authService;
        if (authService != null)
        {
            userProfileService = new UserProfileService(authService);
            IsAuthenticated = true;
            TeacherName = authService.TeacherName;
            Task.Run(LoadProfileImageAsync);
        }
        else
        {
            // Don't authenticate here - let MainWindowViewModel handle it
            // Just set initial state
            IsAuthenticated = false;
            TeacherName = "Not Authenticated";
        }
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

                // Initial populate based on current query (may be empty)
                await ApplySearchImmediate();
                UpdateDisplayLevelBasedOnItemCount();
            }
        }
        catch (Exception ex)
        {
            // In a real application, you would want to log this error
            // and possibly show a user-friendly message
            System.Diagnostics.Debug.WriteLine($"Error loading students: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectStudent(IPerson? person)
    {
        // SelectStudent called
        
        if (person is AddStudentCard)
        {
            // Handle add student card click
            AddStudent();
            return;
        }
        
        if (person is Student student)
        {
            SelectedStudent = student;
            // Reset force grid view when a student is selected
            ForceGridView = false;
        }
    }

    [RelayCommand]
    private void EditStudent(Student student)
    {
        EditStudentRequested?.Invoke(this, student);
    }

    [RelayCommand]
    private void ChangeImage(Student student)
    {
        StudentImageChangeRequested?.Invoke(this, student);
    }

    [RelayCommand]
    private void DeselectStudent()
    {
        SelectedStudent = null;
    }

    [RelayCommand]
    private void DeleteStudent(Student? student)
    {
        if (student == null) return;

        try
        {
            // Clear selection first to prevent binding errors
            if (SelectedStudent?.Id == student.Id)
            {
                SelectedStudent = null;
            }

            // Remove from all collections
            allStudents.Remove(student);
            
            // Remove from current filtered view
            var studentInView = Students.OfType<Student>().FirstOrDefault(s => s.Id == student.Id);
            if (studentInView != null)
            {
                Students.Remove(studentInView);
            }

            // Update display properties immediately to prevent UI issues
            OnPropertyChanged(nameof(ShowSingleStudent));
            OnPropertyChanged(nameof(ShowMultipleStudents));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(FirstStudent));

            // Delay display level update to allow UI to stabilize after deletion
            Dispatcher.UIThread.Post(() => 
            {
                var studentCount = Students?.OfType<Student>().Count() ?? 0;
                var newLevel = CalculateOptimalDisplayLevelFast(studentCount);
                
                if (newLevel != CurrentDisplayLevel)
                {
                    CurrentDisplayLevel = newLevel;
                    DisplayConfig = ProfileCardDisplayConfig.GetConfig(newLevel);
                    OnPropertyChanged(nameof(DisplayConfig));
                    
                    // Force UI to update card layout with new dimensions
                    Dispatcher.UIThread.Post(() => 
                    {
                        OnPropertyChanged(nameof(DisplayConfig));
                    }, DispatcherPriority.Render);
                }
            }, DispatcherPriority.Background);

            // Save changes to JSON in background (don't wait for it)
            _ = Task.Run(async () => await SaveAllStudentsToJson());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting student: {ex.Message}");
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Reset force grid view when user starts a new search
        if (ForceGridView && !string.IsNullOrEmpty(value))
        {
            ForceGridView = false;
        }
        _ = ApplySearchDebounced();
    }

    partial void OnStudentsChanged(ObservableCollection<IPerson> value)
    {
        // Clear selection when students collection changes to avoid binding issues
        if (SelectedStudent != null && !Students.Contains(SelectedStudent))
        {
            SelectedStudent = null;
        }
        
        // Update display properties immediately (no delay for better performance)
        OnPropertyChanged(nameof(ShowSingleStudent));
        OnPropertyChanged(nameof(ShowMultipleStudents));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(FirstStudent));
    }

    partial void OnForceGridViewChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSingleStudent));
        OnPropertyChanged(nameof(ShowMultipleStudents));
    }

    /// <summary>
    /// Called when the window is resized to recalculate display levels
    /// </summary>
    public void OnWindowResized()
    {
        UpdateDisplayLevelBasedOnItemCount();
    }

    /// <summary>
    /// Triggers a card layout update in the UI
    /// </summary>
    public void TriggerCardLayoutUpdate()
    {
        // Triggering card layout update
        // This will be handled by the UI layer
    }

    /// <summary>
    /// Triggers event re-wiring for ProfileCard instances
    /// </summary>
    public void TriggerEventRewiring()
    {
        // This will be handled by the UI layer
        OnPropertyChanged(nameof(Students));
    }

    private void UpdateDisplayLevelBasedOnItemCount()
    {
        // Don't change display level if we're forcing grid view
        if (ForceGridView)
        {
            // ForceGridView is true, skipping display level update
            return;
        }

        // Count only actual students, not the add card
        var studentCount = Students?.OfType<Student>().Count() ?? 0;
        
        // Calculate optimal display level based on both student count and available space
        var newLevel = CalculateOptimalDisplayLevel(studentCount);

        if (newLevel != CurrentDisplayLevel)
        {
            // Updating display level
            CurrentDisplayLevel = newLevel;
            DisplayConfig = ProfileCardDisplayConfig.GetConfig(newLevel);
            
            // Notify property changes after display level update
            OnPropertyChanged(nameof(DisplayConfig));
        }
    }

    /// <summary>
    /// Fast display level calculation for delete operations (no window size calculation)
    /// </summary>
    private ProfileCardDisplayLevel CalculateOptimalDisplayLevelFast(int studentCount)
    {
        return cardSizeManager.DetermineSizeByCount(studentCount);
    }

    /// <summary>
    /// Calculates the optimal display level based on student count and available view space
    /// </summary>
    private ProfileCardDisplayLevel CalculateOptimalDisplayLevel(int studentCount)
    {
        // Get the current view width from the main window
        var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
            ? desktop.MainWindow 
            : null;
        
        var windowWidth = mainWindow?.Width ?? 1200; // Default fallback width
        return cardSizeManager.DetermineOptimalSize(studentCount, windowWidth);
    }


    private System.Threading.CancellationTokenSource? searchCts;

    private Task ApplySearchImmediate()
    {
        try
        {
            var results = searchService.Search(allStudents, SearchText).ToList();
            
            // Clear current collection and add results
            Students.Clear();
            foreach (var s in results)
            {
                Students.Add(s);
            }
            
            // Always add the AddStudentCard as the last item
            Students.Add(new AddStudentCard());
            
            // Trigger property changes for view mode
            OnPropertyChanged(nameof(ShowSingleStudent));
            OnPropertyChanged(nameof(ShowMultipleStudents));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(FirstStudent));
            
            // Trigger event re-wiring after a short delay to ensure UI is updated
            Dispatcher.UIThread.Post(() => TriggerEventRewiring(), DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
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
        catch (TaskCanceledException)
        {
            // ignore
        }
    }


    [RelayCommand]
    private void AddStudent()
    {
        AddStudentRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void BackToGallery()
    {
        // BackToGallery command executed
        // Clear search to show all students
        SearchText = string.Empty;
        // Also deselect the current student to ensure we exit single student mode
        SelectedStudent = null;
        // Force grid view to prevent single student mode
        ForceGridView = true;
        // Set display level to Medium to maintain consistent card sizes
        CurrentDisplayLevel = ProfileCardDisplayLevel.Medium;
        DisplayConfig = ProfileCardDisplayConfig.GetConfig(ProfileCardDisplayLevel.Medium);
        // Search cleared, student deselected, grid view forced
        // This will trigger ApplySearchImmediate which will show all students
    }

    [RelayCommand]
    private void SetDisplayLevel(ProfileCardDisplayLevel level)
    {
        CurrentDisplayLevel = level;
        DisplayConfig = ProfileCardDisplayConfig.GetConfig(level);
    }

    public async Task AddNewStudentAsync(string name, string className, List<string> mentors, string email, DateTime enrollmentDate, string picturePath)
    {
        try
        {
            var newStudent = new Student
            {
                Id = await GenerateNextStudentIdAsync(),
                Name = name,
                ClassName = className,
                Email = email,
                EnrollmentDate = enrollmentDate,
                PictureUrl = picturePath ?? string.Empty
            };

            // Add mentors
            foreach (var mentor in mentors ?? new List<string>())
            {
                newStudent.AddMentor(mentor);
            }

            allStudents.Add(newStudent);
            await ApplySearchImmediate();
            await SaveAllStudentsToJson();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding new student: {ex.Message}");
        }
    }

    public async Task UpdateExistingStudentAsync(Student student, string name, string className, List<string> mentors, string email, DateTime enrollmentDate, string picturePath)
    {
        try
        {
            UpdateStudentProperties(student, name, className, mentors, email, enrollmentDate, picturePath);

            // Ensure the AllStudents collection references the same object or update it
            var inAll = allStudents.FirstOrDefault(s => s.Id == student.Id);
            if (inAll != null && inAll != student)
            {
                UpdateStudentProperties(inAll, name, className, mentors, email, enrollmentDate, picturePath);
            }

            await ApplySearchImmediate();
            await SaveAllStudentsToJson();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating student: {ex.Message}");
        }
    }

    private static void UpdateStudentProperties(Student student, string name, string className, List<string> mentors, string email, DateTime enrollmentDate, string picturePath)
    {
        student.Name = name;
        student.ClassName = className;
        student.Email = email;
        student.EnrollmentDate = enrollmentDate;
        student.PictureUrl = picturePath ?? string.Empty;
        
        // Update mentors
        student.ClearMentors();
        foreach (var mentor in mentors ?? new List<string>())
        {
            student.AddMentor(mentor);
        }
    }

    private async Task<int> GenerateNextStudentIdAsync()
    {
        var all = await LoadAllStudentsFromJson();
        var maxId = all.Any() ? all.Max(s => s.Id) : 0;
        return maxId + 1;
    }

    private async Task SaveAllStudentsToJson()
    {
        try
        {
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "students.json");
            var list = allStudents.ToList();
            var jsonContent = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(jsonPath, jsonContent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving students collection: {ex.Message}");
        }
    }

    public async Task UpdateStudentImage(Student student, string newImagePath)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"UpdateStudentImage called for student {student.Id} with path: {newImagePath}");
            
            // Ensure the saved image file is readable before we update bindings
            await WaitForReadableFileAsync(newImagePath);

            // Helper method to clear cache and update picture URL
            void UpdateStudentPictureUrl(Student s, string oldPath, string newPath)
            {
                if (!string.IsNullOrWhiteSpace(oldPath))
                {
                    UniversalImageConverter.ClearCache(oldPath);
                }
                UniversalImageConverter.ClearCache(newPath);
                s.PictureUrl = newPath;
            }

            // Update the passed student object
            UpdateStudentPictureUrl(student, student.PictureUrl, newImagePath);

            // Update in current filtered collection
            var studentInCollection = Students.OfType<Student>().FirstOrDefault(s => s.Id == student.Id);
            if (studentInCollection != null && studentInCollection != student)
            {
                UpdateStudentPictureUrl(studentInCollection, studentInCollection.PictureUrl, newImagePath);
            }

            // Update in full collection
            var inAll = allStudents.FirstOrDefault(s => s.Id == student.Id);
            if (inAll != null && inAll != student)
            {
                UpdateStudentPictureUrl(inAll, inAll.PictureUrl, newImagePath);
            }
            
            await SaveAllStudentsToJson();
            System.Diagnostics.Debug.WriteLine("UpdateStudentImage completed successfully");
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
                if (stream.Length > 0)
                {
                    return true;
                }
            }
            catch
            {
                // ignore
            }
            await Task.Delay(delayMs);
        }
        return false;
    }

    // Removed per new unified save method

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

    [RelayCommand]
    private async Task Login()
    {
        try
        {
            // Login command triggered
            
            // Always create new auth service for login
            var newAuthService = new GoogleAuthService();
            
            // Add retry logic for login
            bool isAuthenticated = false;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    isAuthenticated = await newAuthService.AuthenticateAsync();
                    if (isAuthenticated) break;
                }
                catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                {
                    // Login attempt failed due to file lock, retrying
                    if (attempt < 2)
                    {
                        await Task.Delay(300 * (attempt + 1)); // Exponential backoff
                        continue;
                    }
                    throw;
                }
            }
            
            if (isAuthenticated)
            {
                // Assign to instance fields so LoadProfileImageAsync can access them
                this.authService = newAuthService;
                this.userProfileService = new UserProfileService(newAuthService);
                
                IsAuthenticated = true;
                TeacherName = newAuthService.TeacherName;
                await LoadProfileImageAsync();
                // Login successful
            }
            else
            {
                // Login failed - authentication unsuccessful
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Logout()
    {
        try
        {
            // Logout command triggered
            if (authService != null)
            {
                authService.ClearCredentials();
                // Reset all authentication-related fields
                authService = null;
                userProfileService = null;
                IsAuthenticated = false;
                TeacherName = "Unknown Teacher";
                ProfileImage = null;
                // User logged out successfully
            }
            else
            {
                // AuthService is null, but still resetting UI state
                IsAuthenticated = false;
                TeacherName = "Unknown Teacher";
                ProfileImage = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Logout error: {ex.Message}");
        }
    }

    private async Task LoadProfileImageAsync()
    {
        try
        {
            if (userProfileService != null)
            {
                // Loading profile image
                var (profileImage, statusMessage) = await userProfileService.LoadProfileImageAsync();
                
                // Ensure UI thread update
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ProfileImage = profileImage;
                    // Profile image loaded
                });
            }
            else
            {
                // UserProfileService is null, cannot load profile image
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading profile image: {ex.Message}");
            // Set profile image to null on error to clear any stale image
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProfileImage = null;
            });
        }
    }


}
