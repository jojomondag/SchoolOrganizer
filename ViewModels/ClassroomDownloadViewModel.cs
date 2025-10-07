using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Apis.Classroom.v1.Data;
using SchoolOrganizer.Services;
using SchoolOrganizer.Services.Utilities;
using Serilog;

namespace SchoolOrganizer.ViewModels;

public partial class ClassroomDownloadViewModel : ObservableObject
{
    private readonly GoogleAuthService _authService;
    private ClassroomDataService? _classroomService;
    private CachedClassroomDataService? _cachedClassroomService;
    private DownloadManager? _downloadManager;

    [ObservableProperty]
    private ObservableCollection<CourseWrapper> _classrooms = new();

    [ObservableProperty]
    private CourseWrapper? _selectedCourse;

    [ObservableProperty]
    private string _statusText = "Select a download folder to get started";

    [ObservableProperty]
    private string _selectedFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    public string TeacherName => _authService.TeacherName;

    public ClassroomDownloadViewModel(GoogleAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        if (_authService.ClassroomService != null)
        {
            _classroomService = new ClassroomDataService(_authService.ClassroomService);
            _cachedClassroomService = new CachedClassroomDataService(_classroomService);
            // Initialize on UI thread
            Dispatcher.UIThread.Post(() => _ = LoadClassroomsAsync());
        }
    }

    public async Task LoadClassroomsAsync()
    {
        try
        {
            if (_cachedClassroomService == null)
            {
                Dispatcher.UIThread.Post(() => StatusText = "Classroom service not initialized. Please authenticate first.");
                return;
            }

            Dispatcher.UIThread.Post(() => StatusText = "Loading courses...");
            var courses = await _cachedClassroomService.GetActiveClassroomsAsync();
            Log.Information($"Retrieved {courses.Count} courses from API");

            // Create CourseWrapper objects on UI thread to avoid threading issues
            var courseWrappers = new List<CourseWrapper>();
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var course in courses)
                {
                    courseWrappers.Add(new CourseWrapper(course, this));
                }
                Classrooms = new ObservableCollection<CourseWrapper>(courseWrappers);
                StatusText = $"Found {Classrooms.Count} courses. Select a course to download.";
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading classrooms");
            Dispatcher.UIThread.Post(() => StatusText = $"Error loading classrooms: {ex.Message}");
        }
    }

    public void SelectFolder(string folderPath)
    {
        if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
        {
            SelectedFolderPath = folderPath;
            StatusText = $"Download folder set to: {folderPath}";

            // Reinitialize download manager with new folder path
            InitializeDownloadManager();
        }
    }

    private void InitializeDownloadManager()
    {
        if (_cachedClassroomService == null || _authService.DriveService == null)
        {
            StatusText = "Services not initialized. Cannot download assignments.";
            Log.Error("Cannot initialize DownloadManager: Services not initialized");
            return;
        }

        if (string.IsNullOrEmpty(SelectedFolderPath))
        {
            StatusText = "Please select a download folder first.";
            Log.Error("Cannot initialize DownloadManager: No folder selected");
            return;
        }

        if (string.IsNullOrWhiteSpace(TeacherName))
        {
            StatusText = "Teacher name not loaded. Please ensure you are authenticated.";
            Log.Error($"Cannot initialize DownloadManager: TeacherName is '{TeacherName}'");
            return;
        }

        try
        {
            Log.Information($"Initializing DownloadManager with folder: {SelectedFolderPath}, teacher: {TeacherName}");
            _downloadManager = new DownloadManager(
                _cachedClassroomService,
                _authService.DriveService,
                SelectedFolderPath,
                TeacherName,
                message => Dispatcher.UIThread.Post(() => StatusText = message)
            );
            Log.Information("DownloadManager initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing DownloadManager");
            StatusText = $"Error initializing DownloadManager: {ex.Message}";
        }
    }

    public async Task DownloadAssignmentsAsync(CourseWrapper courseWrapper)
    {
        if (courseWrapper?.Course == null)
        {
            StatusText = "Course is null.";
            Log.Error("CourseWrapper or Course is null in DownloadAssignmentsAsync");
            return;
        }

        try
        {
            Log.Information($"Starting download for course: {courseWrapper.Course.Name}");

            // Initialize download manager if needed
            if (_downloadManager == null)
            {
                Log.Information("Initializing DownloadManager");
                InitializeDownloadManager();

                if (_downloadManager == null)
                {
                    StatusText = "Failed to initialize download manager.";
                    Log.Error("DownloadManager initialization failed");
                    return;
                }
            }

            StatusText = $"Downloading assignments for {courseWrapper.Course.Name}...";
            Log.Information($"Calling DownloadManager.DownloadAssignmentsAsync for {courseWrapper.Course.Name}");

            // Run the download in a background task to avoid UI thread blocking
            await Task.Run(async () => await _downloadManager.DownloadAssignmentsAsync(courseWrapper.Course));

            Log.Information($"Download completed, updating status for {courseWrapper.Course.Name}");
            Dispatcher.UIThread.Post(() => {
                courseWrapper.UpdateDownloadStatus(true);
                courseWrapper.UpdateFolderStatus();
                StatusText = $"Download completed for {courseWrapper.Course.Name}.";
            });
            Log.Information($"Successfully completed download for {courseWrapper.Course.Name}");
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => StatusText = $"Error downloading assignments: {ex.Message}");
            Log.Error(ex, $"Error during download for {courseWrapper?.Course?.Name ?? "Unknown"}");
        }
    }

    public void OpenCourseFolder(CourseWrapper courseWrapper)
    {
        if (courseWrapper?.CourseFolderPath == null)
        {
            StatusText = "Course folder path not found.";
            return;
        }

        try
        {
            if (Directory.Exists(courseWrapper.CourseFolderPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = courseWrapper.CourseFolderPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
                StatusText = $"Opened folder: {courseWrapper.CourseFolderPath}";
            }
            else
            {
                StatusText = $"Course folder does not exist. Please download the course first.";
                Log.Warning($"Course folder does not exist: {courseWrapper.CourseFolderPath}");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening folder: {ex.Message}";
            Log.Error(ex, "Error opening course folder");
        }
    }

    public void ViewCourseContent(CourseWrapper courseWrapper)
    {
        if (courseWrapper?.Course == null || courseWrapper.CourseFolderPath == null)
        {
            StatusText = "Course information not available.";
            return;
        }

        try
        {
            if (!Directory.Exists(courseWrapper.CourseFolderPath))
            {
                StatusText = $"Course folder does not exist. Please download the course first.";
                return;
            }

            var contentViewModel = new ContentViewModel(
                courseWrapper.Course.Name ?? "Unknown Course",
                courseWrapper.CourseFolderPath,
                null
            );

            var contentView = new Views.ContentView.ContentView
            {
                DataContext = contentViewModel
            };

            contentView.Show();
            StatusText = $"Opened content view for {courseWrapper.Course.Name}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening content view: {ex.Message}";
            Log.Error(ex, "Error opening content view");
        }
    }
}

public partial class CourseWrapper : ObservableObject
{
    public Course Course { get; }
    private readonly ClassroomDownloadViewModel _viewModel;
    private bool _isDownloaded;
    private bool _hasFolder;
    private string _courseFolderPath;
    private bool _hasNewSubmissions;
    private bool _isSelected;
    private bool _isMouseOver;

    public bool HasNewSubmissions
    {
        get => _hasNewSubmissions;
        set => SetProperty(ref _hasNewSubmissions, value);
    }

    public bool IsMouseOver
    {
        get => _isMouseOver;
        set => SetProperty(ref _isMouseOver, value);
    }

    public CourseWrapper(Course course, ClassroomDownloadViewModel viewModel)
    {
        Course = course;
        _viewModel = viewModel;
        _isDownloaded = false; // Initialize as not downloaded
        _courseFolderPath = Path.Combine(
            _viewModel.SelectedFolderPath,
            DirectoryUtil.GetCourseDirectoryName(
                Course.Name ?? "Unknown Course",
                Course.Section ?? "No Section",
                Course.Id ?? "Unknown ID",
                _viewModel.TeacherName)
        );
        
        UpdateFolderStatus(); // Check if folder exists
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        await _viewModel.DownloadAssignmentsAsync(this);
    }

    [RelayCommand]
    private void OpenCourseData()
    {
        _viewModel.OpenCourseFolder(this);
    }

    public bool IsDownloaded
    {
        get => _isDownloaded;
        set => SetProperty(ref _isDownloaded, value);
    }

    public bool HasFolder
    {
        get => _hasFolder;
        set => SetProperty(ref _hasFolder, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string CourseFolderPath => _courseFolderPath;

    public void UpdateFolderStatus()
    {
        HasFolder = Directory.Exists(_courseFolderPath);
        OnPropertyChanged(nameof(HasFolder));
        OnPropertyChanged(nameof(IsDownloaded));
    }

    public void UpdateDownloadStatus(bool isDownloaded)
    {
        IsDownloaded = isDownloaded;
    }
}
