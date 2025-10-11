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
    private bool forceGridView = false;

    [ObservableProperty]
    private Bitmap? profileImage;

    [ObservableProperty]
    private string teacherName = "Unknown Teacher";

    [ObservableProperty]
    private bool isAuthenticated = false;

    // Properties for controlling view mode
    public bool ShowSingleStudent => !ForceGridView && Students.Count == 2 && Students.Any(s => s is Student);
    public bool ShowMultipleStudents => ForceGridView || Students.Count != 2 || !Students.Any(s => s is Student);
    public bool ShowEmptyState => Students.Count == 1 && !Students.Any(s => s is Student);
    public Student? FirstStudent => Students.OfType<Student>().FirstOrDefault();

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

                await ApplySearchImmediate();
                UpdateDisplayLevelBasedOnItemCount();
            }
        }
        catch (Exception ex)
        {
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
    private void EditStudent(Student student) => EditStudentRequested?.Invoke(this, student);

    [RelayCommand]
    private void ChangeImage(Student student) => StudentImageChangeRequested?.Invoke(this, student);

    [RelayCommand]
    private void DeselectStudent() => SelectedStudent = null;

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
        if (ForceGridView) return;

        var studentCount = Students?.OfType<Student>().Count() ?? 0;
        var newLevel = CalculateOptimalDisplayLevel(studentCount);

        if (newLevel != CurrentDisplayLevel)
        {
            CurrentDisplayLevel = newLevel;
            DisplayConfig = ProfileCardDisplayConfig.GetConfig(newLevel);
            OnPropertyChanged(nameof(DisplayConfig));
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
            
            Students.Clear();
            foreach (var s in results)
                Students.Add(s);
            Students.Add(new AddStudentCard());
            
            UpdateViewProperties();
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
        catch (TaskCanceledException) { }
    }


    [RelayCommand]
    private void AddStudent() => AddStudentRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void BackToGallery()
    {
        SearchText = string.Empty;
        SelectedStudent = null;
        ForceGridView = true;
        CurrentDisplayLevel = ProfileCardDisplayLevel.Medium;
        DisplayConfig = ProfileCardDisplayConfig.GetConfig(ProfileCardDisplayLevel.Medium);
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

            foreach (var mentor in mentors ?? new List<string>())
                newStudent.AddMentor(mentor);

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
            var nextId = await GenerateNextStudentIdAsync();

            foreach (var studentData in students)
            {
                var newStudent = new Student
                {
                    Id = nextId++,
                    Name = studentData.Name,
                    ClassName = studentData.ClassName,
                    Email = studentData.Email,
                    EnrollmentDate = studentData.EnrollmentDate,
                    PictureUrl = studentData.PicturePath ?? string.Empty
                };

                foreach (var mentor in studentData.Mentors ?? new List<string>())
                    newStudent.AddMentor(mentor);

                newStudents.Add(newStudent);
            }

            foreach (var student in newStudents)
            {
                allStudents.Add(student);
            }

            await ApplySearchImmediate();
            await SaveAllStudentsToJson();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding multiple students: {ex.Message}");
        }
    }

    public async Task UpdateExistingStudentAsync(Student student, string name, string className, List<string> mentors, string email, DateTime enrollmentDate, string picturePath)
    {
        try
        {
            UpdateStudentProperties(student, name, className, mentors, email, enrollmentDate, picturePath);

            var inAll = allStudents.FirstOrDefault(s => s.Id == student.Id);
            if (inAll != null && inAll != student)
                UpdateStudentProperties(inAll, name, className, mentors, email, enrollmentDate, picturePath);

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
        
        student.ClearMentors();
        foreach (var mentor in mentors ?? new List<string>())
            student.AddMentor(mentor);
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


}
