using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolOrganizer.Src.Views.Windows.ImageCrop;
using SchoolOrganizer.Src.Models.Students;
using SchoolOrganizer.Src.Services;
using Google.Apis.Classroom.v1.Data;
using StudentModel = SchoolOrganizer.Src.Models.Students.Student;

namespace SchoolOrganizer.Src.Views.Windows;

public partial class SimplifiedAddStudentWindow : BaseDialogWindow
{
    public enum WindowMode { Manual, FromClassroom }

    public partial class AddStudentState : ObservableObject
    {
        public ObservableCollection<string> AvailableClasses { get; } = new();
        public ObservableCollection<string> AvailableTeachers { get; } = new();
        public ObservableCollection<string> SelectedTeachers { get; } = new();
        public ObservableCollection<ClassroomWrapper> AvailableClassrooms { get; } = new();
        public ObservableCollection<ClassroomStudentGroup> ClassroomStudentGroups { get; } = new();
        public ObservableCollection<ClassroomStudentWrapper> ClassroomStudents { get; } = new();
        
        [ObservableProperty]
        private WindowMode windowMode = WindowMode.Manual;
        
        [ObservableProperty]
        private bool isLoadingClassrooms = false;
        
        [ObservableProperty]
        private bool isLoadingStudents = false;
        
        [ObservableProperty]
        private string selectedImagePath = string.Empty;

        public bool IsManualMode 
        { 
            get => WindowMode == WindowMode.Manual; 
            set { if (value) WindowMode = WindowMode.Manual; }
        }
        
        public bool IsClassroomMode 
        { 
            get => WindowMode == WindowMode.FromClassroom; 
            set { if (value) WindowMode = WindowMode.FromClassroom; }
        }

        public bool IsImageMissing => string.IsNullOrWhiteSpace(SelectedImagePath);
        
        [RelayCommand]
        private void SwitchToManualMode() => WindowMode = WindowMode.Manual;
        
        [RelayCommand]
        private void SwitchToClassroomMode() => WindowMode = WindowMode.FromClassroom;
        
        public void SelectAllClassroomStudents()
        {
            foreach (var group in ClassroomStudentGroups)
            {
                foreach (var student in group.Students)
                    student.IsSelected = true;
            }
        }
        
        public void DeselectAllClassroomStudents()
        {
            foreach (var group in ClassroomStudentGroups)
            {
                foreach (var student in group.Students)
                    student.IsSelected = false;
            }
        }
        
        public List<ClassroomStudentWrapper> GetSelectedClassroomStudents()
        {
            return ClassroomStudentGroups
                .SelectMany(g => g.Students)
                .Where(s => s.IsSelected)
                .ToList();
        }
    }

    private readonly AddStudentState _state = new();
    private GoogleAuthService? _authService;
    private ClassroomDataService? _classroomService;
    private ImageDownloadService? _imageDownloadService;

    // UI Controls
    private TextBox? _nameBox;
    private ComboBox? _classBox;
    private ComboBox? _teacherBox;
    private TextBox? _emailBox;
    private DatePicker? _enrollmentPicker;
    private ComboBox? _classroomBox;
    private ItemsControl? _selectedTeachersList;
    private TextBlock? _validationText;
    private Button? _primaryButton;

    public AddedStudentResult? Result { get; private set; }

    public SimplifiedAddStudentWindow()
    {
        InitializeComponent();
        DataContext = _state;
        InitializeControls();
        SetupEventHandlers();
    }

    public SimplifiedAddStudentWindow(GoogleAuthService authService) : this()
    {
        _authService = authService;
        _classroomService = new ClassroomDataService(authService.ClassroomService!);
        _imageDownloadService = new ImageDownloadService();
        
        _state.PropertyChanged += OnStatePropertyChanged;
        _ = LoadClassroomsAsync();
    }

    private void InitializeControls()
    {
        _nameBox = this.FindControl<TextBox>("NameBox");
        _classBox = this.FindControl<ComboBox>("ClassBox");
        _teacherBox = this.FindControl<ComboBox>("TeacherBox");
        _emailBox = this.FindControl<TextBox>("EmailBox");
        _enrollmentPicker = this.FindControl<DatePicker>("EnrollmentPicker");
        _classroomBox = this.FindControl<ComboBox>("ClassroomBox");
        _selectedTeachersList = this.FindControl<ItemsControl>("SelectedTeachersList");
        _validationText = this.FindControl<TextBlock>("ValidationText");
        _primaryButton = this.FindControl<Button>("PrimaryButton");
        
        _enrollmentPicker!.SelectedDate = DateTimeOffset.Now;
    }

    private void SetupEventHandlers()
    {
        // Teacher management
        this.FindControl<Button>("AddTeacherButton")?.AddHandler(Button.ClickEvent, OnAddTeacherClick);
        _teacherBox?.AddHandler(ComboBox.SelectionChangedEvent, OnTeacherSelected);
        
        // Classroom management
        this.FindControl<Button>("SelectAllStudentsButton")?.AddHandler(Button.ClickEvent, OnSelectAllStudentsClick);
        this.FindControl<Button>("DeselectAllStudentsButton")?.AddHandler(Button.ClickEvent, OnDeselectAllStudentsClick);
        
        // Main actions
        this.FindControl<Button>("CancelButton")?.AddHandler(Button.ClickEvent, OnCancelClick);
        _primaryButton?.AddHandler(Button.ClickEvent, OnAddClick);
    }

    private void OnStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AddStudentState.WindowMode) && 
            _state.WindowMode == WindowMode.FromClassroom &&
            _classroomService != null && 
            _state.AvailableClassrooms.Count == 0)
        {
            _ = LoadClassroomsAsync();
        }
    }

    public void InitializeForEdit(StudentModel student)
    {
        Title = "Edit Student";
        _primaryButton!.Content = "Save";

        _nameBox!.Text = student.Name;
        _classBox!.SelectedItem = student.ClassName;
        
        _state.SelectedTeachers.Clear();
        foreach (var teacher in student.Teachers)
            _state.SelectedTeachers.Add(teacher);
        
        _emailBox!.Text = student.Email;
        _enrollmentPicker!.SelectedDate = new DateTimeOffset(student.EnrollmentDate);
        _state.SelectedImagePath = student.PictureUrl;
    }

    public void LoadOptionsFromStudents(IEnumerable<StudentModel> students)
    {
        _state.AvailableClasses.Clear();
        _state.AvailableTeachers.Clear();

        foreach (var cls in students.Select(s => s.ClassName).Distinct())
        {
            if (!string.IsNullOrWhiteSpace(cls)) 
                _state.AvailableClasses.Add(cls);
        }
        
        var allTeachers = students.SelectMany(s => s.Teachers).Distinct();
        foreach (var teacher in allTeachers)
        {
            if (!string.IsNullOrWhiteSpace(teacher)) 
                _state.AvailableTeachers.Add(teacher);
        }
    }

    private async void OnChooseImageClick(object? sender, RoutedEventArgs e)
    {
        var path = await ConsolidatedImageCropWindow.ShowAsync(this);
        if (!string.IsNullOrWhiteSpace(path))
        {
            _state.SelectedImagePath = path;
        }
    }

    private void OnAddTeacherClick(object? sender, RoutedEventArgs e)
    {
        _teacherBox!.IsVisible = true;
        _teacherBox.IsDropDownOpen = true;
    }

    private void OnTeacherSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_teacherBox!.SelectedItem is string selectedTeacher && 
            !string.IsNullOrWhiteSpace(selectedTeacher) &&
            !_state.SelectedTeachers.Contains(selectedTeacher))
        {
            _state.SelectedTeachers.Add(selectedTeacher);
        }
        
        _teacherBox.SelectedItem = null;
        _teacherBox.IsVisible = false;
    }

    private void OnRemoveTeacherClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string teacherToRemove)
        {
            _state.SelectedTeachers.Remove(teacherToRemove);
        }
    }

    private async Task LoadClassroomsAsync()
    {
        if (_classroomService == null) return;

        try
        {
            _state.IsLoadingClassrooms = true;
            var classrooms = await _classroomService.GetActiveClassroomsAsync();
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _state.AvailableClassrooms.Clear();
                foreach (var classroom in classrooms)
                {
                    var wrapper = new ClassroomWrapper(classroom);
                    wrapper.PropertyChanged += OnClassroomToggled;
                    _state.AvailableClassrooms.Add(wrapper);
                }
            });
        }
        catch (Exception)
        {
            // Silently handle classroom loading errors
        }
        finally
        {
            _state.IsLoadingClassrooms = false;
        }
    }

    private async void OnClassroomToggled(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ClassroomWrapper.IsToggled) || sender is not ClassroomWrapper wrapper)
            return;

        System.Diagnostics.Debug.WriteLine($"Classroom toggled: {wrapper.Name}, IsToggled: {wrapper.IsToggled}");

        if (wrapper.IsToggled)
        {
            await LoadStudentsForClassroom(wrapper);
        }
        else
        {
            RemoveStudentsForClassroom(wrapper);
        }
    }

    private async Task LoadStudentsForClassroom(ClassroomWrapper classroomWrapper)
    {
        if (_classroomService == null) return;

        try
        {
            _state.IsLoadingStudents = true;
            
            var students = await _classroomService.GetStudentsInCourseAsync(classroomWrapper.Id);
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var group = new ClassroomStudentGroup(classroomWrapper.Name, classroomWrapper.Id);
                
                foreach (var student in students)
                {
                    var wrapper = new ClassroomStudentWrapper(student);
                    wrapper.IsSelected = true; // Auto-select students when classroom is toggled on
                    group.Students.Add(wrapper);
                }
                
                _state.ClassroomStudentGroups.Add(group);
                
                // Update the flat list for backward compatibility
                _state.ClassroomStudents.Clear();
                foreach (var g in _state.ClassroomStudentGroups)
                {
                    foreach (var s in g.Students)
                        _state.ClassroomStudents.Add(s);
                }
            });
        }
        catch (Exception)
        {
            // Silently handle student loading errors
        }
        finally
        {
            _state.IsLoadingStudents = false;
        }
    }

    private void RemoveStudentsForClassroom(ClassroomWrapper classroomWrapper)
    {
        var groupToRemove = _state.ClassroomStudentGroups
            .FirstOrDefault(g => g.ClassroomId == classroomWrapper.Id);
        
        if (groupToRemove != null)
        {
            _state.ClassroomStudentGroups.Remove(groupToRemove);
            
            // Update the flat list for backward compatibility
            _state.ClassroomStudents.Clear();
            foreach (var g in _state.ClassroomStudentGroups)
            {
                foreach (var s in g.Students)
                    _state.ClassroomStudents.Add(s);
            }
        }
    }

    private void OnSelectAllStudentsClick(object? sender, RoutedEventArgs e)
    {
        _state.SelectAllClassroomStudents();
    }

    private void OnDeselectAllStudentsClick(object? sender, RoutedEventArgs e)
    {
        _state.DeselectAllClassroomStudents();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (_state.WindowMode == WindowMode.Manual)
        {
            if (string.IsNullOrWhiteSpace(_nameBox!.Text))
            {
                _validationText!.Text = "Name is required";
                return;
            }

            Result = new AddedStudentResult
            {
                Name = _nameBox.Text,
                ClassName = (_classBox!.SelectedItem as string) ?? string.Empty,
                Teachers = new List<string>(_state.SelectedTeachers),
                Email = _emailBox!.Text ?? string.Empty,
                EnrollmentDate = _enrollmentPicker!.SelectedDate?.DateTime ?? DateTime.Now,
                PicturePath = _state.SelectedImagePath
            };
        }
        else
        {
            OnImportFromClassroomClick(sender, e);
        }
        
        Close();
    }

    private async void OnImportFromClassroomClick(object? sender, RoutedEventArgs e)
    {
        var selectedStudents = _state.GetSelectedClassroomStudents();
        if (selectedStudents.Count == 0)
        {
            _validationText!.Text = "Please select at least one student to import";
            return;
        }

        if (_state.ClassroomStudentGroups.Count == 0)
        {
            _validationText!.Text = "Please toggle at least one classroom";
            return;
        }

        try
        {
            var results = new List<AddedStudentResult>();
            var teacherName = _authService?.TeacherName ?? "Unknown Teacher";

            foreach (var group in _state.ClassroomStudentGroups)
            {
                var selectedStudentsInGroup = group.Students.Where(s => s.IsSelected).ToList();
                
                foreach (var wrapper in selectedStudentsInGroup)
                {
                    var student = wrapper.ClassroomStudent;
                    var result = new AddedStudentResult
                    {
                        Name = student.Profile?.Name?.FullName ?? "Unknown Student",
                        ClassName = group.ClassroomName,
                        Email = student.Profile?.EmailAddress ?? string.Empty,
                        EnrollmentDate = DateTime.Now,
                        Teachers = new List<string> { teacherName },
                        PicturePath = string.Empty
                    };

                    if (!string.IsNullOrEmpty(wrapper.ProfilePhotoUrl) && _imageDownloadService != null)
                    {
                        var localPath = await _imageDownloadService.DownloadProfileImageAsync(
                            wrapper.ProfilePhotoUrl, 
                            result.Name);
                        result.PicturePath = localPath;
                    }

                    results.Add(result);
                }
            }

            // For now, just use the first result - could be extended to handle multiple
            Result = results.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _validationText!.Text = $"Error importing students: {ex.Message}";
        }
    }

    protected override T? GetResult<T>() where T : class
    {
        if (typeof(T) == typeof(AddedStudentResult))
        {
            return Result as T;
        }
        return null;
    }

    public class AddedStudentResult
    {
        public string Name { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public List<string> Teachers { get; set; } = new();
        public string Email { get; set; } = string.Empty;
        public DateTime EnrollmentDate { get; set; }
        public string PicturePath { get; set; } = string.Empty;
    }

    private void OnClassroomCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is ClassroomWrapper classroomWrapper)
        {
            System.Diagnostics.Debug.WriteLine($"Toggling classroom: {classroomWrapper.Name} from {classroomWrapper.IsToggled} to {!classroomWrapper.IsToggled}");
            classroomWrapper.IsToggled = !classroomWrapper.IsToggled;
            
            // Update the CSS class for visual feedback
            if (classroomWrapper.IsToggled)
            {
                border.Classes.Add("toggled");
                System.Diagnostics.Debug.WriteLine($"Added 'toggled' class to classroom: {classroomWrapper.Name}");
            }
            else
            {
                border.Classes.Remove("toggled");
                System.Diagnostics.Debug.WriteLine($"Removed 'toggled' class from classroom: {classroomWrapper.Name}");
            }
            
            // Debug: Print current classes
            var classes = string.Join(", ", border.Classes);
            System.Diagnostics.Debug.WriteLine($"Current classes for {classroomWrapper.Name}: {classes}");
        }
    }
}
