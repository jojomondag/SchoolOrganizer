using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Avalonia.Input;
using SchoolOrganizer.Src.Views.Windows.ImageCrop;
using SchoolOrganizer.Src.Models.Students;
using SchoolOrganizer.Src.Models.UI;
using SchoolOrganizer.Src.Services;
using Google.Apis.Classroom.v1.Data;

namespace SchoolOrganizer.Src.Views.Windows;

public partial class AddStudentWindow : Window
{
    public enum Mode { Add, Edit }

    public enum WindowMode
    {
        Manual,
        FromClassroom
    }

    public partial class AddStudentState : ObservableObject
    {
        public ObservableCollection<string> AvailableClasses { get; } = new();
        public ObservableCollection<string> AvailableTeachers { get; } = new();
        public ObservableCollection<string> SelectedTeachers { get; } = new();
        
        // Mode switching properties
        [ObservableProperty]
        private WindowMode windowMode = WindowMode.Manual;
        
        // Tab binding properties
        public bool IsManualMode 
        { 
            get => WindowMode == WindowMode.Manual; 
            set 
            { 
                if (value && WindowMode != WindowMode.Manual)
                {
                    WindowMode = WindowMode.Manual;
                }
            }
        }
        
        public bool IsClassroomMode 
        { 
            get => WindowMode == WindowMode.FromClassroom; 
            set 
            { 
                if (value && WindowMode != WindowMode.FromClassroom)
                {
                    WindowMode = WindowMode.FromClassroom;
                }
            }
        }
        
        // Commands for tab switching
        [RelayCommand]
        private void SwitchToManualMode()
        {
            WindowMode = WindowMode.Manual;
        }
        
        [RelayCommand]
        private void SwitchToClassroomMode()
        {
            WindowMode = WindowMode.FromClassroom;
        }
        
        // Classroom import properties
        public ObservableCollection<Google.Apis.Classroom.v1.Data.Course> AvailableClassrooms { get; } = new();
        public ObservableCollection<ClassroomStudentWrapper> ClassroomStudents { get; } = new();
        
        [ObservableProperty]
        private Google.Apis.Classroom.v1.Data.Course? selectedClassroom;
        
        [ObservableProperty]
        private bool isLoadingClassrooms = false;
        
        [ObservableProperty]
        private bool isLoadingStudents = false;
        
        private string selectedImagePath = string.Empty;
        public string SelectedImagePath
        {
            get => selectedImagePath;
            set
            {
                if (SetProperty(ref selectedImagePath, value))
                {
                    OnPropertyChanged(nameof(IsImageMissing));
                }
            }
        }

        public bool IsImageMissing => string.IsNullOrWhiteSpace(SelectedImagePath);
        
        // Helper methods for classroom mode
        public void SelectAllClassroomStudents()
        {
            foreach (var student in ClassroomStudents)
            {
                student.IsSelected = true;
            }
        }
        
        public void DeselectAllClassroomStudents()
        {
            foreach (var student in ClassroomStudents)
            {
                student.IsSelected = false;
            }
        }
        
        public List<ClassroomStudentWrapper> GetSelectedClassroomStudents()
        {
            return ClassroomStudents.Where(s => s.IsSelected).ToList();
        }
        
        // Override OnPropertyChanged to handle computed properties
        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            
            if (e.PropertyName == nameof(WindowMode))
            {
                OnPropertyChanged(nameof(IsManualMode));
                OnPropertyChanged(nameof(IsClassroomMode));
            }
        }
    }

    private readonly AddStudentState state = new();
    private GoogleAuthService? authService;
    private ClassroomDataService? classroomService;
    private ImageDownloadService? imageDownloadService;

    public string StudentName => NameBox.Text ?? string.Empty;
    public string StudentClass => (ClassBox.SelectedItem as string) ?? string.Empty;
    public System.Collections.Generic.List<string> StudentTeachers => new(state.SelectedTeachers);
    public string StudentEmail => EmailBox.Text ?? string.Empty;
    public DateTime? EnrollmentDate => EnrollmentPicker.SelectedDate?.DateTime;
    public string SelectedImagePath => state.SelectedImagePath;

    public AddStudentWindow()
    {
        InitializeComponent();
        DataContext = state;
        EnrollmentPicker.SelectedDate = DateTimeOffset.Now;
        
        // Add Escape key handling to close the window
        this.KeyDown += OnKeyDown;
        
        // Add hover effects to profile image border
        Loaded += OnWindowLoaded;
    }

    public AddStudentWindow(GoogleAuthService authService) : this()
    {
        this.authService = authService;
        this.classroomService = new ClassroomDataService(authService.ClassroomService!);
        this.imageDownloadService = new ImageDownloadService();
        
        // Subscribe to mode changes to trigger classroom loading
        state.PropertyChanged += OnStatePropertyChanged;
        
        // Load classrooms when initialized with auth service
        _ = LoadClassroomsAsync();
    }
    
    private void OnStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AddStudentState.WindowMode) && 
            state.WindowMode == WindowMode.FromClassroom &&
            classroomService != null && 
            state.AvailableClassrooms.Count == 0)
        {
            _ = LoadClassroomsAsync();
        }
    }

    public void InitializeForEdit(SchoolOrganizer.Src.Models.Students.Student student)
    {
        TitleText.Text = "Edit Student";
        PrimaryButton.Content = "Save";

        NameBox.Text = student.Name;
        ClassBox.SelectedItem = student.ClassName;
        
        // Load multiple teachers
        state.SelectedTeachers.Clear();
        foreach (var teacher in student.Teachers)
        {
            state.SelectedTeachers.Add(teacher);
        }
        
        EmailBox.Text = student.Email;
        EnrollmentPicker.SelectedDate = new DateTimeOffset(student.EnrollmentDate);
        state.SelectedImagePath = student.PictureUrl;
        // Store the context student to enable preloading original image when clicking avatar
        Tag = student;
    }

    public void LoadOptionsFromStudents(System.Collections.Generic.IEnumerable<SchoolOrganizer.Src.Models.Students.Student> students)
    {
        state.AvailableClasses.Clear();
        state.AvailableTeachers.Clear();

        foreach (var cls in System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(students, s => s.ClassName)))
        {
            if (!string.IsNullOrWhiteSpace(cls)) state.AvailableClasses.Add(cls);
        }
        
        // Extract all teachers from all students
        var allTeachers = students.SelectMany(s => s.Teachers).Distinct();
        foreach (var teacher in allTeachers)
        {
            if (!string.IsNullOrWhiteSpace(teacher)) state.AvailableTeachers.Add(teacher);
        }
    }

    private async void OnChooseImageClick(object? sender, RoutedEventArgs e)
    {
        var parentWindow = this;
        string? path = await ImageCropWindow.ShowAsync(parentWindow);
        if (!string.IsNullOrWhiteSpace(path))
        {
            state.SelectedImagePath = path;
            // Force a refresh in case the file isn't immediately readable
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await System.Threading.Tasks.Task.Delay(150);
                var current = state.SelectedImagePath;
                if (string.Equals(current, path, StringComparison.Ordinal))
                {
                    state.SelectedImagePath = string.Empty;
                    await System.Threading.Tasks.Task.Delay(1);
                    state.SelectedImagePath = path;
                }
            });
        }
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        // Find the profile image border and add hover effects
        if (this.FindControl<Border>("ProfileImageBorder") is { } profileImageBorder)
        {
            profileImageBorder.PointerEntered += OnProfileImagePointerEntered;
            profileImageBorder.PointerExited += OnProfileImagePointerExited;
        }
    }

    private void OnProfileImagePointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (sender is Border profileImageBorder)
        {
            // Use the same shadow as BaseProfileCard for consistency
            if (this.FindResource("ShadowStrong") is Avalonia.Media.BoxShadows hoverShadow)
                profileImageBorder.BoxShadow = hoverShadow;
        }
    }

    private void OnProfileImagePointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (sender is Border profileImageBorder)
        {
            // Use the same shadow as BaseProfileCard for consistency
            if (this.FindResource("ShadowLight") is Avalonia.Media.BoxShadows normalShadow)
                profileImageBorder.BoxShadow = normalShadow;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (state.WindowMode == WindowMode.Manual)
        {
            if (string.IsNullOrWhiteSpace(StudentName))
            {
                ValidationText.Text = "Name is required";
                return;
            }

            Close(new AddedStudentResult
            {
                Name = StudentName,
                ClassName = StudentClass,
                Teachers = StudentTeachers,
                Email = StudentEmail,
                EnrollmentDate = EnrollmentDate ?? DateTime.Now,
                PicturePath = SelectedImagePath
            });
        }
        else if (state.WindowMode == WindowMode.FromClassroom)
        {
            OnImportFromClassroomClick(sender, e);
        }
    }

    private void OnAddTeacherClick(object? sender, RoutedEventArgs e)
    {
        TeacherBox.IsVisible = true;
        TeacherBox.IsDropDownOpen = true;
    }

    private void OnTeacherSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (TeacherBox.SelectedItem is string selectedTeacher && 
            !string.IsNullOrWhiteSpace(selectedTeacher) &&
            !state.SelectedTeachers.Contains(selectedTeacher))
        {
            state.SelectedTeachers.Add(selectedTeacher);
        }
        
        TeacherBox.SelectedItem = null;
        TeacherBox.IsVisible = false;
    }

    private void OnRemoveTeacherClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string teacherToRemove)
        {
            state.SelectedTeachers.Remove(teacherToRemove);
        }
    }

    // Mode switching handlers - now handled by commands in AddStudentState

    private void UpdateButtonText()
    {
        PrimaryButton.Content = state.WindowMode == WindowMode.Manual ? "Add Student" : "Import Selected Students";
    }

    // Classroom loading methods
    private async Task LoadClassroomsAsync()
    {
        if (classroomService == null) return;

        try
        {
            state.IsLoadingClassrooms = true;
            var classrooms = await classroomService.GetActiveClassroomsAsync();
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                state.AvailableClassrooms.Clear();
                foreach (var classroom in classrooms)
                {
                    state.AvailableClassrooms.Add(classroom);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading classrooms: {ex.Message}");
        }
        finally
        {
            state.IsLoadingClassrooms = false;
        }
    }

    private async void OnClassroomSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (state.SelectedClassroom == null || classroomService == null) return;

        try
        {
            state.IsLoadingStudents = true;
            state.ClassroomStudents.Clear();
            
            var students = await classroomService.GetStudentsInCourseAsync(state.SelectedClassroom.Id);
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var student in students)
                {
                    state.ClassroomStudents.Add(new ClassroomStudentWrapper(student));
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading students: {ex.Message}");
        }
        finally
        {
            state.IsLoadingStudents = false;
        }
    }

    // Classroom student selection handlers
    private void OnSelectAllStudentsClick(object? sender, RoutedEventArgs e)
    {
        state.SelectAllClassroomStudents();
    }

    private void OnDeselectAllStudentsClick(object? sender, RoutedEventArgs e)
    {
        state.DeselectAllClassroomStudents();
    }

    // Import from classroom handler
    private async void OnImportFromClassroomClick(object? sender, RoutedEventArgs e)
    {
        var selectedStudents = state.GetSelectedClassroomStudents();
        if (selectedStudents.Count == 0)
        {
            ValidationText.Text = "Please select at least one student to import";
            return;
        }

        if (state.SelectedClassroom == null)
        {
            ValidationText.Text = "Please select a classroom";
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
                    ClassName = state.SelectedClassroom.Name ?? "Unknown Class",
                    Email = student.Profile?.EmailAddress ?? string.Empty,
                    EnrollmentDate = DateTime.Now,
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

            Close(results);
        }
        catch (Exception ex)
        {
            ValidationText.Text = $"Error importing students: {ex.Message}";
        }
    }

    public class AddedStudentResult
    {
        public string Name { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> Teachers { get; set; } = new();
        public string Email { get; set; } = string.Empty;
        public DateTime EnrollmentDate { get; set; }
        public string PicturePath { get; set; } = string.Empty;
    }
}


