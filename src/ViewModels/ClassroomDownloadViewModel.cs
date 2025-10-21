using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Apis.Classroom.v1.Data;
using SchoolOrganizer.Src.Services;
using SchoolOrganizer.Src.Services.Utilities;
using Serilog;

namespace SchoolOrganizer.Src.ViewModels;

public partial class ClassroomDownloadViewModel : ObservableObject
{
    private readonly GoogleAuthService _authService;
    private ClassroomDataService? _classroomService;
    private CachedClassroomDataService? _cachedClassroomService;
    private DownloadManager? _downloadManager;
    private System.Threading.Timer? _autoSyncTimer;
    private readonly List<CourseWrapper> _autoSyncCourses = new();
    private readonly object _autoSyncLock = new();

    [ObservableProperty]
    private ObservableCollection<CourseWrapper> _classrooms = new();

    [ObservableProperty]
    private CourseWrapper? _selectedCourse;

    [ObservableProperty]
    private string _statusText = "Select a download folder to get started";

    [ObservableProperty]
    private string _selectedFolderPath = SettingsService.Instance.LoadDownloadFolderPath();

    [ObservableProperty]
    private bool _isDownloadSectionExpanded = true;

    [ObservableProperty]
    private bool _hasFolderSelected = false;

    [ObservableProperty]
    private bool _isShowingContent = false;

    [ObservableProperty]
    private ContentViewModel? _currentContentViewModel;

    public string TeacherName => _authService.TeacherName;

    [RelayCommand]
    private void ToggleDownloadSection()
    {
        IsDownloadSectionExpanded = !IsDownloadSectionExpanded;
    }

    public void ResetToClassroomList()
    {
        IsShowingContent = false;
        CurrentContentViewModel = null;
        StatusText = "Select a course to download or view content.";
        
        // Refresh course status when returning to classroom list
        RefreshAllCourseFolderStatus();
    }

    /// <summary>
    /// Refreshes the folder status for all courses to update the UI
    /// </summary>
    private async void RefreshAllCourseFolderStatus(bool forceRefresh = false)
    {
        // Run folder existence checks in parallel to avoid blocking UI
        var tasks = Classrooms.Select(async course => 
        {
            await Task.Run(() => course.UpdateFolderStatus(forceRefresh));
        });
        
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Public method to refresh all course folder status - useful for external calls
    /// </summary>
    public void RefreshCourseStatus(bool forceRefresh = false)
    {
        RefreshAllCourseFolderStatus(forceRefresh);
    }


    public ClassroomDownloadViewModel(GoogleAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        // Update status to show loaded folder path
        if (!string.IsNullOrEmpty(SelectedFolderPath) && SelectedFolderPath != Environment.GetFolderPath(Environment.SpecialFolder.Desktop))
        {
            StatusText = $"Using saved download folder: {SelectedFolderPath}";
            HasFolderSelected = true;
            IsDownloadSectionExpanded = false; // Auto-collapse if folder is already set
        }

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
            HasFolderSelected = true;
            IsDownloadSectionExpanded = false; // Auto-collapse after folder selection

            // Save the selected folder path for future use
            SettingsService.Instance.SaveDownloadFolderPath(folderPath);

            // Reinitialize download manager with new folder path
            InitializeDownloadManager();
            
            // Refresh folder status for all courses since the folder path changed
            RefreshAllCourseFolderStatus();
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
            await Task.Run(async () => await _downloadManager.DownloadAssignmentsAsync(courseWrapper.Course, incrementalSync: false));

            Log.Information($"Download completed, updating status for {courseWrapper.Course.Name}");
            Dispatcher.UIThread.Post(() => {
                courseWrapper.UpdateDownloadStatus(true);
                courseWrapper.UpdateFolderStatus();
                StatusText = $"Download completed for {courseWrapper.Course.Name}.";

                // Refresh folder status for all courses to update UI
                RefreshAllCourseFolderStatus();
            });
            Log.Information($"Successfully completed download for {courseWrapper.Course.Name}");
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => StatusText = $"Error downloading assignments: {ex.Message}");
            Log.Error(ex, $"Error during download for {courseWrapper?.Course?.Name ?? "Unknown"}");
        }
    }

    public async Task SyncAssignmentsAsync(CourseWrapper courseWrapper)
    {
        if (courseWrapper?.Course == null)
        {
            StatusText = "Course is null.";
            Log.Error("CourseWrapper or Course is null in SyncAssignmentsAsync");
            return;
        }

        try
        {
            Log.Information($"Starting sync for course: {courseWrapper.Course.Name}");

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

            StatusText = $"Syncing assignments for {courseWrapper.Course.Name}...";
            Log.Information($"Calling DownloadManager.DownloadAssignmentsAsync (incremental) for {courseWrapper.Course.Name}");

            // Run the sync in a background task to avoid UI thread blocking
            await Task.Run(async () => await _downloadManager.DownloadAssignmentsAsync(courseWrapper.Course, incrementalSync: true));

            Log.Information($"Sync completed, updating status for {courseWrapper.Course.Name}");
            Dispatcher.UIThread.Post(() => {
                courseWrapper.UpdateDownloadStatus(true);
                courseWrapper.UpdateFolderStatus();
                StatusText = $"Sync completed for {courseWrapper.Course.Name}.";

                // Refresh folder status for all courses to update UI
                RefreshAllCourseFolderStatus();
            });
            Log.Information($"Successfully completed sync for {courseWrapper.Course.Name}");
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => StatusText = $"Error syncing assignments: {ex.Message}");
            Log.Error(ex, $"Error during sync for {courseWrapper?.Course?.Name ?? "Unknown"}");
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
            // Show UI immediately, check folder existence asynchronously
            var contentViewModel = new ContentViewModel(
                courseWrapper.Course.Name ?? "Unknown Course",
                courseWrapper.CourseFolderPath
            );

            CurrentContentViewModel = contentViewModel;
            IsShowingContent = true;
            StatusText = $"Viewing content for {courseWrapper.Course.Name}";

            // Check folder existence asynchronously and update status if needed
            _ = Task.Run(() =>
            {
                if (!Directory.Exists(courseWrapper.CourseFolderPath))
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Course folder does not exist. Please download the course first.";
                    });
                }
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening content view: {ex.Message}";
            Log.Error(ex, "Error opening content view");
        }
    }

    public void RegisterCourseForAutoSync(CourseWrapper courseWrapper)
    {
        lock (_autoSyncLock)
        {
            if (!_autoSyncCourses.Contains(courseWrapper))
            {
                _autoSyncCourses.Add(courseWrapper);
                Log.Information($"Registered {courseWrapper.Course.Name} for auto-sync");

                // Start timer if this is the first course
                if (_autoSyncCourses.Count == 1)
                {
                    StartAutoSyncTimer();
                }
            }
        }
    }

    public void UnregisterCourseFromAutoSync(CourseWrapper courseWrapper)
    {
        lock (_autoSyncLock)
        {
            if (_autoSyncCourses.Remove(courseWrapper))
            {
                Log.Information($"Unregistered {courseWrapper.Course.Name} from auto-sync");

                // Stop timer if no more courses
                if (_autoSyncCourses.Count == 0)
                {
                    StopAutoSyncTimer();
                }
            }
        }
    }

    private void StartAutoSyncTimer()
    {
        // Sync every 30 minutes
        var intervalMs = 30 * 60 * 1000;
        _autoSyncTimer = new System.Threading.Timer(AutoSyncTimerCallback, null, intervalMs, intervalMs);
        Log.Information("Started auto-sync timer (30 minute interval)");
    }

    private void StopAutoSyncTimer()
    {
        _autoSyncTimer?.Dispose();
        _autoSyncTimer = null;
        Log.Information("Stopped auto-sync timer");
    }

    private async void AutoSyncTimerCallback(object? state)
    {
        List<CourseWrapper> coursesToSync;
        lock (_autoSyncLock)
        {
            coursesToSync = _autoSyncCourses.ToList();
        }

        foreach (var course in coursesToSync)
        {
            try
            {
                Log.Information($"Auto-syncing {course.Course.Name}");
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    course.IsSyncing = true;
                    await SyncAssignmentsAsync(course);
                    course.IsSyncing = false;
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error during auto-sync for {course.Course.Name}");
            }
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
    private DateTime _lastFolderCheck = DateTime.MinValue;
    private static readonly TimeSpan FolderCheckCacheDuration = TimeSpan.FromSeconds(5);

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

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private bool _isAutoSyncEnabled;

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
    private async Task SyncAsync()
    {
        // Toggle auto-sync for this course
        IsAutoSyncEnabled = !IsAutoSyncEnabled;

        if (IsAutoSyncEnabled)
        {
            // Perform initial sync
            try
            {
                IsSyncing = true;
                await _viewModel.SyncAssignmentsAsync(this);
            }
            finally
            {
                IsSyncing = false;
            }

            // Register for automatic periodic syncing
            _viewModel.RegisterCourseForAutoSync(this);
        }
        else
        {
            // Unregister from automatic syncing
            _viewModel.UnregisterCourseFromAutoSync(this);
        }
    }

    [RelayCommand]
    private void OpenCourseData()
    {
        _viewModel.ViewCourseContent(this);
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

    public void UpdateFolderStatus(bool forceRefresh = false)
    {
        // Only check folder existence if cache has expired or forced refresh
        if (forceRefresh || DateTime.Now - _lastFolderCheck > FolderCheckCacheDuration)
        {
            HasFolder = Directory.Exists(_courseFolderPath);
            _lastFolderCheck = DateTime.Now;
            
            // If folder exists, assume it was downloaded (set IsDownloaded to true)
            if (HasFolder)
            {
                IsDownloaded = true;
            }
            
            OnPropertyChanged(nameof(HasFolder));
            OnPropertyChanged(nameof(IsDownloaded));
        }
    }

    public void UpdateDownloadStatus(bool isDownloaded)
    {
        IsDownloaded = isDownloaded;
    }
}
