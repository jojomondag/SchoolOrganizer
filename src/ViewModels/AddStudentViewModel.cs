using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolOrganizer.Src.Models.Students;
using SchoolOrganizer.Src.Services;
using Google.Apis.Classroom.v1.Data;

namespace SchoolOrganizer.Src.ViewModels;

public partial class AddStudentViewModel : ObservableObject
{

    // Events for completion
    public event EventHandler<AddedStudentResult>? StudentAdded;
    public event EventHandler<List<AddedStudentResult>>? MultipleStudentsAdded;
    public event EventHandler? Cancelled;

    // Mode switching
    [ObservableProperty]
    private bool isManualMode = true;
    
    [ObservableProperty]
    private bool isClassroomMode = false;

    [ObservableProperty]
    private bool isEditMode = false;

    // Manual entry properties
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
    public ObservableCollection<Course> AvailableClassrooms { get; } = new();
    public ObservableCollection<ClassroomStudentWrapper> ClassroomStudents { get; } = new();
    
    [ObservableProperty]
    private Course? selectedClassroom;
    
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
                EnrollmentDate = EnrollmentDate.DateTime,
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
    private void RemoveTeacher(string teacher)
    {
        SelectedTeachers.Remove(teacher);
    }

    [RelayCommand]
    private void SelectAllClassroomStudents()
    {
        foreach (var student in ClassroomStudents)
        {
            student.IsSelected = true;
        }
    }

    [RelayCommand]
    private void DeselectAllClassroomStudents()
    {
        foreach (var student in ClassroomStudents)
        {
            student.IsSelected = false;
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
                AvailableClassrooms.Add(classroom);
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
    private async Task OnClassroomSelected()
    {
        if (SelectedClassroom == null) return;
        
        if (classroomService == null)
        {
            ValidationText = "Please login with Google to load students";
            return;
        }

        try
        {
            IsLoadingStudents = true;
            ValidationText = "Loading students...";
            ClassroomStudents.Clear();
            
            var students = await classroomService.GetStudentsInCourseAsync(SelectedClassroom.Id);
            
            foreach (var student in students)
            {
                ClassroomStudents.Add(new ClassroomStudentWrapper(student));
            }
            
            // Automatically select all students when classroom is selected
            foreach (var student in ClassroomStudents)
            {
                student.IsSelected = true;
            }
            
            if (students.Count == 0)
            {
                ValidationText = "No students found in this classroom";
            }
            else
            {
                ValidationText = $"Found {students.Count} students. All students are selected for import.";
            }
        }
        catch (Exception ex)
        {
            ValidationText = $"Error loading students: {ex.Message}";
        }
        finally
        {
            IsLoadingStudents = false;
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

        if (SelectedClassroom == null)
        {
            ValidationText = "Please select a classroom";
            return;
        }

        try
        {
            var results = new List<AddedStudentResult>();
            var teacherName = authService?.TeacherName ?? "Unknown Teacher";

            foreach (var wrapper in selectedStudents)
            {
                var student = wrapper.ClassroomStudent;
                var result = new AddedStudentResult
                {
                    Name = student.Profile?.Name?.FullName ?? "Unknown Student",
                    ClassName = SelectedClassroom.Name ?? "Unknown Class",
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
        else if (e.PropertyName == nameof(SelectedClassroom))
        {
            // Automatically load students when classroom is selected
            _ = OnClassroomSelected();
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
        
        coordinator.ManualEntryRequested += OnManualEntryRequested;
        coordinator.ClassroomImportRequested += OnClassroomImportRequested;
    }

    /// <summary>
    /// Handles manual entry request from coordinator service
    /// </summary>
    private void OnManualEntryRequested(object? sender, EventArgs e)
    {
        SwitchToManualMode();
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
