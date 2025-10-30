using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolOrganizer.Src.Models.Students;
using SchoolOrganizer.Src.Models.UI;
using SchoolOrganizer.Src.Services;
using Google.Apis.Classroom.v1.Data;

namespace SchoolOrganizer.Src.ViewModels;

public partial class AddStudentViewModel : ObservableObject
{

    // Events for completion
    public event EventHandler<AddedStudentResult>? StudentAdded;
    public event EventHandler<List<AddedStudentResult>>? MultipleStudentsAdded;
    public event EventHandler? Cancelled;

    // Display configuration properties - initialized with defaults to prevent null binding errors
    [ObservableProperty]
    private ProfileCardDisplayLevel currentDisplayLevel = ProfileCardDisplayLevel.Medium;

    [ObservableProperty]
    private ProfileCardDisplayConfig displayConfig = ProfileCardDisplayConfig.GetConfig(ProfileCardDisplayLevel.Medium);

    // Mode switching
    [ObservableProperty]
    private bool isManualMode = false;

    [ObservableProperty]
    private bool isClassroomMode = true;

    [ObservableProperty]
    private bool isEditMode = false;

    // Properties used for edit mode and student data
    [ObservableProperty]
    private string studentName = string.Empty;
    
    [ObservableProperty]
    private string selectedClassName = string.Empty;
    
    [ObservableProperty]
    private string studentEmail = string.Empty;
    
    [ObservableProperty]
    private DateTimeOffset enrollmentDate = DateTimeOffset.Now;
    
    [ObservableProperty]
    private string selectedImagePath = string.Empty;

    [ObservableProperty]
    private string? cropSettings;

    [ObservableProperty]
    private string? originalImagePath;

    public bool IsImageMissing => string.IsNullOrWhiteSpace(SelectedImagePath);

    // Teacher management
    public ObservableCollection<string> AvailableTeachers { get; } = new();
    public ObservableCollection<string> SelectedTeachers { get; } = new();

    // Classroom import properties
    public ObservableCollection<ClassroomWrapper> AvailableClassrooms { get; } = new();
    public ObservableCollection<ClassroomStudentWrapper> ClassroomStudents { get; } = new();
    public ObservableCollection<ClassroomStudentGroup> GroupedClassroomStudents { get; } = new();
    
    [ObservableProperty]
    private bool isLoadingClassrooms = false;
    
    [ObservableProperty]
    private bool isLoadingStudents = false;

    // Available classes for dropdown
    public ObservableCollection<string> AvailableClasses { get; } = new();

    // Validation
    [ObservableProperty]
    private string validationText = string.Empty;

    // Button text
    public string PrimaryButtonText 
    {
        get
        {
            if (IsEditMode)
                return "Save Changes";
            return IsManualMode ? "Add Student" : "Import Selected Students";
        }
    }

    // Services
    private GoogleAuthService? authService;
    private ClassroomDataService? classroomService;
    private ImageDownloadService? imageDownloadService;

    public AddStudentViewModel(GoogleAuthService? authService = null)
    {
        this.authService = authService;
        if (authService != null)
        {
            this.classroomService = new ClassroomDataService(authService.ClassroomService!);
            this.imageDownloadService = new ImageDownloadService();
            
            // Load classrooms when initialized with auth service
            _ = LoadClassroomsAsync();
        }
        
        // Subscribe to coordinator events for mode switching
        SubscribeToCoordinatorEvents();
    }

    /// <summary>
    /// Updates the authentication service and initializes classroom loading
    /// This method can be called when authentication completes after the ViewModel is created
    /// </summary>
    public void UpdateAuthService(GoogleAuthService authService)
    {
        this.authService = authService;
        this.classroomService = new ClassroomDataService(authService.ClassroomService!);
        this.imageDownloadService = new ImageDownloadService();
        
        // Load classrooms when auth service is updated
        _ = LoadClassroomsAsync();
    }

    // Commands for mode switching
    [RelayCommand]
    private void SwitchToManualMode()
    {
        IsManualMode = true;
        IsClassroomMode = false;
    }

    [RelayCommand]
    private void SwitchToClassroomMode()
    {
        IsManualMode = false;
        IsClassroomMode = true;
        
        // Ensure classrooms are loaded when switching to classroom mode
        if (classroomService != null && AvailableClassrooms.Count == 0)
        {
            _ = LoadClassroomsAsync();
        }
    }

    [RelayCommand]
    private async Task AddStudent()
    {
        if (IsManualMode)
        {
            if (string.IsNullOrWhiteSpace(StudentName))
            {
                ValidationText = "Name is required";
                return;
            }

            var result = new AddedStudentResult
            {
                Name = StudentName,
                ClassName = SelectedClassName,
                Teachers = new List<string>(SelectedTeachers),
                Email = StudentEmail,
                EnrollmentDate = EnrollmentDate,
                PicturePath = SelectedImagePath
            };

            StudentAdded?.Invoke(this, result);
        }
        else
        {
            await ImportFromClassroom();
        }
    }

    [RelayCommand]
    private void RemoveTeacher(string teacher)
    {
        SelectedTeachers.Remove(teacher);
    }

    [RelayCommand]
    private async Task ImportFromClassroom()
    {
        await ImportFromClassroomAsync();
    }

    [RelayCommand]
    private void Cancel()
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SelectAllClassroomStudents()
    {
        foreach (var student in ClassroomStudents)
        {
            student.IsSelected = true;
        }
        UpdateValidationText();
    }

    [RelayCommand]
    private void DeselectAllClassroomStudents()
    {
        foreach (var student in ClassroomStudents)
        {
            student.IsSelected = false;
        }
        UpdateValidationText();
    }

    [RelayCommand]
    private void OpenGoogleClassroomHelp()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://support.google.com/edu/classroom",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ValidationText = $"Error opening help: {ex.Message}";
        }
    }

    // Classroom import methods
    private async Task LoadClassroomsAsync()
    {
        
        if (classroomService == null) 
        {
            // Show message that authentication is required
            ValidationText = "Please login with Google to import from Classroom";
            return;
        }

        try
        {
            IsLoadingClassrooms = true;
            ValidationText = "Loading classrooms...";
            var classrooms = await classroomService.GetActiveClassroomsAsync();
            
            AvailableClassrooms.Clear();
            foreach (var classroom in classrooms)
            {
                AvailableClassrooms.Add(new ClassroomWrapper(classroom));
            }
            
            if (classrooms.Count == 0)
            {
                ValidationText = "No active classrooms found";
            }
            else
            {
                ValidationText = string.Empty;
            }
        }
        catch (Exception ex)
        {
            ValidationText = $"Error loading classrooms: {ex.Message}";
        }
        finally
        {
            IsLoadingClassrooms = false;
        }
    }

    [RelayCommand]
    private async Task ToggleClassroom(ClassroomWrapper classroomWrapper)
    {
        if (classroomWrapper == null) return;
        
        // Check if this classroom is already loaded to prevent duplicates
        bool classroomAlreadyLoaded = GroupedClassroomStudents.Any(g => g.ClassroomId == classroomWrapper.ClassroomId);
        
        // Toggle the classroom state
        classroomWrapper.IsToggled = !classroomWrapper.IsToggled;
        
        if (classroomWrapper.IsToggled)
        {
            // Classroom is now toggled ON - load and add its students only if not already loaded
            if (!classroomAlreadyLoaded)
            {
                await LoadStudentsForClassroom(classroomWrapper);
            }
        }
        else
        {
            // Classroom is now toggled OFF - remove its students
            RemoveStudentsFromClassroom(classroomWrapper.ClassroomId);
        }
        
        UpdateValidationText();
    }
    
    private async Task LoadStudentsForClassroom(ClassroomWrapper classroomWrapper)
    {
        if (classroomService == null)
        {
            ValidationText = "Please login with Google to load students";
            return;
        }

        try
        {
            IsLoadingStudents = true;
            ValidationText = $"Loading students from {classroomWrapper.Name}...";
            
            var students = await classroomService.GetStudentsInCourseAsync(classroomWrapper.ClassroomId);
            
            // Create a group for this classroom
            var group = new ClassroomStudentGroup(classroomWrapper.ClassroomId, classroomWrapper.Name);
            
            foreach (var student in students)
            {
                var wrapper = new ClassroomStudentWrapper(student, classroomWrapper.ClassroomId);
                // Automatically select all students when classroom is toggled
                wrapper.IsSelected = true;
                ClassroomStudents.Add(wrapper);
                group.Students.Add(wrapper);
            }
            
            // Add the group to the grouped collection
            GroupedClassroomStudents.Add(group);
        }
        catch (Exception ex)
        {
            ValidationText = $"Error loading students from {classroomWrapper.Name}: {ex.Message}";
            // If there was an error, toggle the classroom back off
            classroomWrapper.IsToggled = false;
        }
        finally
        {
            IsLoadingStudents = false;
        }
    }
    
    private void RemoveStudentsFromClassroom(string classroomId)
    {
        // Remove all students from this classroom (flat list)
        var studentsToRemove = ClassroomStudents.Where(s => s.ClassroomId == classroomId).ToList();
        foreach (var student in studentsToRemove)
        {
            ClassroomStudents.Remove(student);
        }
        
        // Remove the group for this classroom
        var groupToRemove = GroupedClassroomStudents.FirstOrDefault(g => g.ClassroomId == classroomId);
        if (groupToRemove != null)
        {
            GroupedClassroomStudents.Remove(groupToRemove);
        }
    }
    
    private void UpdateValidationText()
    {
        var toggledClassrooms = AvailableClassrooms.Where(c => c.IsToggled).ToList();
        var totalStudents = ClassroomStudents.Count;
        
        if (toggledClassrooms.Count == 0)
        {
            ValidationText = "Select one or more classrooms to view students";
        }
        else if (totalStudents == 0)
        {
            ValidationText = "No students found in selected classrooms";
        }
        else
        {
            var selectedCount = ClassroomStudents.Count(s => s.IsSelected);
            ValidationText = $"{toggledClassrooms.Count} classroom(s) active • {totalStudents} total students • {selectedCount} selected for import";
        }
    }

    private async Task ImportFromClassroomAsync()
    {
        var selectedStudents = ClassroomStudents.Where(s => s.IsSelected).ToList();
        if (selectedStudents.Count == 0)
        {
            ValidationText = "Please select at least one student to import";
            return;
        }

        var toggledClassrooms = AvailableClassrooms.Where(c => c.IsToggled).ToList();
        if (toggledClassrooms.Count == 0)
        {
            ValidationText = "Please toggle at least one classroom";
            return;
        }

        try
        {
            var results = new List<AddedStudentResult>();
            var teacherName = authService?.TeacherName ?? "Unknown Teacher";

            foreach (var wrapper in selectedStudents)
            {
                var student = wrapper.ClassroomStudent;
                
                // Find the classroom this student belongs to
                var classroom = AvailableClassrooms.FirstOrDefault(c => c.ClassroomId == wrapper.ClassroomId);
                var className = classroom?.Name ?? "Unknown Class";
                
                var result = new AddedStudentResult
                {
                    Name = student.Profile?.Name?.FullName ?? "Unknown Student",
                    ClassName = className,
                    Email = student.Profile?.EmailAddress ?? string.Empty,
                    EnrollmentDate = DateTimeOffset.Now,
                    Teachers = new List<string> { teacherName },
                    PicturePath = string.Empty
                };

                // Download profile photo if available
                if (!string.IsNullOrEmpty(wrapper.ProfilePhotoUrl) && imageDownloadService != null)
                {
                    var localPath = await imageDownloadService.DownloadProfileImageAsync(
                        wrapper.ProfilePhotoUrl, 
                        result.Name);
                    result.PicturePath = localPath;
                }

                results.Add(result);
            }

            MultipleStudentsAdded?.Invoke(this, results);
        }
        catch (Exception ex)
        {
            ValidationText = $"Error importing students: {ex.Message}";
        }
    }

    public void LoadOptionsFromStudents(IEnumerable<SchoolOrganizer.Src.Models.Students.Student> students)
    {
        AvailableClasses.Clear();
        AvailableTeachers.Clear();

        foreach (var cls in students.Select(s => s.ClassName).Distinct())
        {
            if (!string.IsNullOrWhiteSpace(cls)) 
                AvailableClasses.Add(cls);
        }
        
        // Extract all teachers from all students
        var allTeachers = students.SelectMany(s => s.Teachers).Distinct();
        foreach (var teacher in allTeachers)
        {
            if (!string.IsNullOrWhiteSpace(teacher)) 
                AvailableTeachers.Add(teacher);
        }
    }

    public void InitializeForEdit(SchoolOrganizer.Src.Models.Students.Student student)
    {
        IsEditMode = true;
        SwitchToManualMode(); // Always use manual mode for editing
        StudentName = student.Name;
        SelectedClassName = student.ClassName;
        
        // Load multiple teachers
        SelectedTeachers.Clear();
        foreach (var teacher in student.Teachers)
        {
            SelectedTeachers.Add(teacher);
        }
        
        StudentEmail = student.Email;
        EnrollmentDate = new DateTimeOffset(student.EnrollmentDate);
        SelectedImagePath = student.PictureUrl;
        CropSettings = student.CropSettings;
        OriginalImagePath = student.OriginalImagePath;
    }

    public void ResetToAddMode()
    {
        IsEditMode = false;
        StudentName = string.Empty;
        SelectedClassName = string.Empty;
        SelectedTeachers.Clear();
        StudentEmail = string.Empty;
        EnrollmentDate = DateTimeOffset.Now;
        SelectedImagePath = string.Empty;
        CropSettings = null;
        OriginalImagePath = null;
    }

    // Override OnPropertyChanged to handle computed properties
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        
        if (e.PropertyName == nameof(SelectedImagePath))
        {
            OnPropertyChanged(nameof(IsImageMissing));
        }
        else if (e.PropertyName == nameof(IsManualMode) || e.PropertyName == nameof(IsClassroomMode) || e.PropertyName == nameof(IsEditMode))
        {
            OnPropertyChanged(nameof(PrimaryButtonText));
        }
    }

    /// <summary>
    /// Subscribes to StudentCoordinatorService events for mode switching
    /// </summary>
    private void SubscribeToCoordinatorEvents()
    {
        var coordinator = Services.StudentCoordinatorService.Instance;
        
        coordinator.ClassroomImportRequested += OnClassroomImportRequested;
    }

    /// <summary>
    /// Handles classroom import request from coordinator service
    /// </summary>
    private void OnClassroomImportRequested(object? sender, EventArgs e)
    {
        SwitchToClassroomMode();
    }

    public class AddedStudentResult
    {
        public string Name { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public List<string> Teachers { get; set; } = new();
        public string Email { get; set; } = string.Empty;
        public DateTimeOffset EnrollmentDate { get; set; }
        public string PicturePath { get; set; } = string.Empty;
    }
}
