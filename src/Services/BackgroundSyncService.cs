using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Google.Apis.Classroom.v1.Data;
using SchoolOrganizer.Src.Services;
using Serilog;

namespace SchoolOrganizer.Src.Services;

/// <summary>
/// Service for automatic background synchronization of classroom assignments
/// </summary>
public class BackgroundSyncService : IDisposable
{
    private readonly GoogleAuthService _authService;
    private readonly SettingsService _settingsService;
    private CachedClassroomDataService? _cachedClassroomService;
    private DownloadManager? _downloadManager;
    private Timer? _syncTimer;
    private bool _isSyncing = false;
    private bool _isEnabled = false;
    private int _syncIntervalMinutes = 30; // Default: 30 minutes
    private DateTime _lastSyncTime = DateTime.MinValue;
    private int _newSubmissionsCount = 0;

    public event EventHandler<SyncStatusEventArgs>? SyncStatusChanged;
    public event EventHandler<int>? NewSubmissionsDetected;

    public bool IsEnabled => _isEnabled;
    public bool IsSyncing => _isSyncing;
    public DateTime LastSyncTime => _lastSyncTime;
    public int SyncIntervalMinutes => _syncIntervalMinutes;
    public int NewSubmissionsCount => _newSubmissionsCount;

    public BackgroundSyncService(GoogleAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _settingsService = SettingsService.Instance;

        // Load sync settings
        LoadSyncSettings();

        // Initialize services
        InitializeServices();
    }

    private void LoadSyncSettings()
    {
        try
        {
            // Load from settings service (you'll need to add these methods to SettingsService)
            _syncIntervalMinutes = _settingsService.GetSyncIntervalMinutes() ?? 30;
            _isEnabled = _settingsService.GetAutoSyncEnabled() ?? false;

            Log.Information($"Loaded sync settings: Interval={_syncIntervalMinutes} minutes, Enabled={_isEnabled}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading sync settings, using defaults");
            _syncIntervalMinutes = 30;
            _isEnabled = false;
        }
    }

    private void InitializeServices()
    {
        try
        {
            if (_authService.ClassroomService != null)
            {
                var classroomService = new ClassroomDataService(_authService.ClassroomService);
                _cachedClassroomService = new CachedClassroomDataService(classroomService);
                Log.Information("Background sync services initialized");
            }
            else
            {
                Log.Warning("Cannot initialize sync services: ClassroomService is null");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing sync services");
        }
    }

    /// <summary>
    /// Starts the background sync service
    /// </summary>
    public void Start()
    {
        if (_isEnabled && _syncTimer == null)
        {
            var intervalMs = _syncIntervalMinutes * 60 * 1000;
            _syncTimer = new Timer(OnTimerElapsed, null, TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(intervalMs));

            RaiseSyncStatusChanged("Background sync started", SyncStatus.Started);
            Log.Information($"Background sync started with {_syncIntervalMinutes} minute interval");
        }
    }

    /// <summary>
    /// Stops the background sync service
    /// </summary>
    public void Stop()
    {
        if (_syncTimer != null)
        {
            _syncTimer.Dispose();
            _syncTimer = null;
            RaiseSyncStatusChanged("Background sync stopped", SyncStatus.Stopped);
            Log.Information("Background sync stopped");
        }
    }

    /// <summary>
    /// Enables or disables automatic sync
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        _settingsService.SaveAutoSyncEnabled(enabled);

        if (enabled)
        {
            Start();
        }
        else
        {
            Stop();
        }

        Log.Information($"Auto sync {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Sets the sync interval in minutes
    /// </summary>
    public void SetSyncInterval(int minutes)
    {
        if (minutes < 5)
        {
            Log.Warning($"Sync interval {minutes} is too short, setting to minimum of 5 minutes");
            minutes = 5;
        }

        _syncIntervalMinutes = minutes;
        _settingsService.SaveSyncIntervalMinutes(minutes);

        // Restart timer with new interval
        if (_isEnabled)
        {
            Stop();
            Start();
        }

        Log.Information($"Sync interval set to {minutes} minutes");
    }

    /// <summary>
    /// Triggers an immediate sync
    /// </summary>
    public async Task SyncNowAsync()
    {
        await PerformSyncAsync();
    }

    private async void OnTimerElapsed(object? state)
    {
        await PerformSyncAsync();
    }

    private async Task PerformSyncAsync()
    {
        if (_isSyncing)
        {
            Log.Information("Sync already in progress, skipping this cycle");
            return;
        }

        try
        {
            _isSyncing = true;
            _newSubmissionsCount = 0;
            RaiseSyncStatusChanged("Starting sync...", SyncStatus.InProgress);

            var downloadFolderPath = _settingsService.LoadDownloadFolderPath();
            if (string.IsNullOrEmpty(downloadFolderPath))
            {
                Log.Warning("No download folder configured, skipping sync");
                RaiseSyncStatusChanged("No download folder configured", SyncStatus.Error);
                return;
            }

            if (_cachedClassroomService == null || _authService.DriveService == null)
            {
                Log.Warning("Services not initialized, skipping sync");
                RaiseSyncStatusChanged("Services not initialized", SyncStatus.Error);
                return;
            }

            // Get all active courses
            var courses = await _cachedClassroomService.GetActiveClassroomsAsync();
            Log.Information($"Found {courses.Count} courses to sync");

            if (courses.Count == 0)
            {
                RaiseSyncStatusChanged("No courses found", SyncStatus.Completed);
                return;
            }

            // Initialize download manager
            _downloadManager = new DownloadManager(
                _cachedClassroomService,
                _authService.DriveService,
                downloadFolderPath,
                _authService.TeacherName,
                message => Log.Debug($"Sync status: {message}")
            );

            // Sync each course
            int updatedCourses = 0;
            foreach (var course in courses)
            {
                try
                {
                    Log.Information($"Syncing course: {course.Name}");
                    RaiseSyncStatusChanged($"Syncing {course.Name}...", SyncStatus.InProgress);

                    // Get last download time for this course
                    var lastDownloadTime = _downloadManager.GetLastDownloadTime(course.Id);

                    if (!lastDownloadTime.HasValue)
                    {
                        // Never downloaded this course before, skip it
                        Log.Information($"Course {course.Name} has never been downloaded, skipping sync");
                        continue;
                    }

                    // Perform incremental sync
                    await _downloadManager.DownloadAssignmentsAsync(course, incrementalSync: true);
                    updatedCourses++;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error syncing course {course.Name}");
                }
            }

            _lastSyncTime = DateTime.Now;
            string statusMessage = updatedCourses > 0
                ? $"Sync completed: {updatedCourses} course(s) updated"
                : "Sync completed: No new submissions";

            RaiseSyncStatusChanged(statusMessage, SyncStatus.Completed);
            Log.Information($"Background sync completed: {updatedCourses} courses updated");

            if (_newSubmissionsCount > 0)
            {
                NewSubmissionsDetected?.Invoke(this, _newSubmissionsCount);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during background sync");
            RaiseSyncStatusChanged($"Sync error: {ex.Message}", SyncStatus.Error);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void RaiseSyncStatusChanged(string message, SyncStatus status)
    {
        Log.Debug($"Sync status: {message} ({status})");
        SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs(message, status, DateTime.Now));
    }

    public void Dispose()
    {
        Stop();
        _syncTimer?.Dispose();
    }
}

public enum SyncStatus
{
    Started,
    InProgress,
    Completed,
    Error,
    Stopped
}

public class SyncStatusEventArgs : EventArgs
{
    public string Message { get; }
    public SyncStatus Status { get; }
    public DateTime Timestamp { get; }

    public SyncStatusEventArgs(string message, SyncStatus status, DateTime timestamp)
    {
        Message = message;
        Status = status;
        Timestamp = timestamp;
    }
}
