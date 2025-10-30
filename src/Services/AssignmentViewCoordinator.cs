using System;
using SchoolOrganizer.Src.ViewModels;
using SchoolOrganizer.Src.Views.AssignmentManagement;
using Serilog;

namespace SchoolOrganizer.Src.Services;

/// <summary>
/// Coordinator service to manage the shared StudentDetailViewModel instance
/// and track whether the assignment view is embedded, detached, or both
/// </summary>
public class AssignmentViewCoordinator
{
    private static AssignmentViewCoordinator? _instance;
    private static readonly object _lock = new();

    public static AssignmentViewCoordinator Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new AssignmentViewCoordinator();
                    }
                }
            }
            return _instance;
        }
    }

    private StudentDetailViewModel? _sharedViewModel;
    private AssignmentViewer? _detachedWindow;
    private bool _isEmbedded;
    private bool _isDetached;
    private bool _preferEmbedded;

    /// <summary>
    /// Gets the shared ViewModel instance. Creates a new one if it doesn't exist.
    /// </summary>
    public StudentDetailViewModel SharedViewModel
    {
        get
        {
            if (_sharedViewModel == null)
            {
                _sharedViewModel = new StudentDetailViewModel();
                Log.Information("Created new shared StudentDetailViewModel");
            }
            return _sharedViewModel;
        }
    }

    /// <summary>
    /// Gets whether the assignment view is currently embedded in the main window
    /// </summary>
    public bool IsEmbedded => _isEmbedded;

    /// <summary>
    /// Gets whether the assignment view is currently detached (in a separate window)
    /// </summary>
    public bool IsDetached => _isDetached;

    /// <summary>
    /// Gets whether any assignment view is currently active (embedded or detached)
    /// </summary>
    public bool IsAnyViewActive => _isEmbedded || _isDetached;

    /// <summary>
    /// Gets or sets whether the user prefers embedded mode (true) or detached mode (false)
    /// </summary>
    public bool PreferEmbedded
    {
        get => _preferEmbedded;
        set
        {
            if (_preferEmbedded != value)
            {
                _preferEmbedded = value;
                
                // Save the preference to settings
                SettingsService.Instance.SaveAssignmentViewPreference(value);
                
                Log.Information("Assignment view preference changed to: {Mode}", value ? "Embedded" : "Detached");
            }
        }
    }

    /// <summary>
    /// Event raised when the view mode changes (embedded/detached state)
    /// </summary>
    public event EventHandler? ViewModeChanged;

    /// <summary>
    /// Event raised when requesting to show the embedded view
    /// </summary>
    public event EventHandler? EmbeddedViewRequested;

    /// <summary>
    /// Event raised when requesting to close the embedded view (navigate back to gallery)
    /// </summary>
    public event EventHandler? EmbeddedViewCloseRequested;

    private AssignmentViewCoordinator()
    {
        _isEmbedded = false;
        _isDetached = false;
        
        // Load the saved preference
        _preferEmbedded = SettingsService.Instance.LoadAssignmentViewPreference();
        
        Log.Information("AssignmentViewCoordinator initialized with preference: {Preference}", 
            _preferEmbedded ? "Embedded" : "Detached");
    }

    /// <summary>
    /// Activates the embedded view mode
    /// </summary>
    public void ActivateEmbeddedView()
    {
        Log.Information("Activating embedded view. Current state - Embedded: {IsEmbedded}, Detached: {IsDetached}", 
            _isEmbedded, _isDetached);

        _isEmbedded = true;
        
        // If detached window is open, we now have both views active
        if (_isDetached)
        {
            Log.Information("Both embedded and detached views are now active");
        }

        ViewModeChanged?.Invoke(this, EventArgs.Empty);
        EmbeddedViewRequested?.Invoke(this, EventArgs.Empty);

        Log.Information("Embedded view activated. New state - Embedded: {IsEmbedded}, Detached: {IsDetached}", 
            _isEmbedded, _isDetached);
    }

    /// <summary>
    /// Deactivates the embedded view mode
    /// </summary>
    public void DeactivateEmbeddedView()
    {
        Log.Information("Deactivating embedded view. Current state - Embedded: {IsEmbedded}, Detached: {IsDetached}", 
            _isEmbedded, _isDetached);

        _isEmbedded = false;
        ViewModeChanged?.Invoke(this, EventArgs.Empty);

        Log.Information("Embedded view deactivated. New state - Embedded: {IsEmbedded}, Detached: {IsDetached}", 
            _isEmbedded, _isDetached);
    }

    /// <summary>
    /// Activates the detached window mode and returns the window instance
    /// </summary>
    public AssignmentViewer ActivateDetachedWindow()
    {
        Log.Information("Activating detached window. Current state - Embedded: {IsEmbedded}, Detached: {IsDetached}", 
            _isEmbedded, _isDetached);

        if (_detachedWindow == null || !_detachedWindow.IsVisible)
        {
            _detachedWindow = new AssignmentViewer(SharedViewModel);
            _detachedWindow.Closed += OnDetachedWindowClosed;
            _isDetached = true;

            Log.Information("Created new detached window");
        }
        else
        {
            Log.Information("Detached window already exists, bringing to front");
            _detachedWindow.Activate();
        }

        ViewModeChanged?.Invoke(this, EventArgs.Empty);

        Log.Information("Detached window activated. New state - Embedded: {IsEmbedded}, Detached: {IsDetached}", 
            _isEmbedded, _isDetached);

        return _detachedWindow;
    }

    /// <summary>
    /// Deactivates the detached window mode and closes the window
    /// </summary>
    public void DeactivateDetachedWindow()
    {
        Log.Information("Deactivating detached window. Current state - Embedded: {IsEmbedded}, Detached: {IsDetached}", 
            _isEmbedded, _isDetached);

        if (_detachedWindow != null)
        {
            _detachedWindow.Closed -= OnDetachedWindowClosed;
            _detachedWindow.Close();
            _detachedWindow = null;
        }

        _isDetached = false;
        ViewModeChanged?.Invoke(this, EventArgs.Empty);

        Log.Information("Detached window deactivated. New state - Embedded: {IsEmbedded}, Detached: {IsDetached}", 
            _isEmbedded, _isDetached);
    }

    /// <summary>
    /// Handles the detached window being closed by the user
    /// </summary>
    private void OnDetachedWindowClosed(object? sender, EventArgs e)
    {
        Log.Information("Detached window closed by user");
        
        if (_detachedWindow != null)
        {
            _detachedWindow.Closed -= OnDetachedWindowClosed;
            _detachedWindow = null;
        }

        _isDetached = false;
        ViewModeChanged?.Invoke(this, EventArgs.Empty);

        Log.Information("Detached window cleanup completed. New state - Embedded: {IsEmbedded}, Detached: {IsDetached}", 
            _isEmbedded, _isDetached);
    }

    /// <summary>
    /// Detaches from embedded mode and attaches to a window
    /// </summary>
    public void DetachFromEmbedded()
    {
        Log.Information("Detaching from embedded view");

        // Update preference - user wants detached mode
        PreferEmbedded = false;

        // Request to close the embedded view (navigate back to gallery)
        EmbeddedViewCloseRequested?.Invoke(this, EventArgs.Empty);

        // Deactivate embedded view
        DeactivateEmbeddedView();

        // Activate detached window
        var window = ActivateDetachedWindow();
        window.Show();

        Log.Information("Successfully detached from embedded to window");
    }

    /// <summary>
    /// Attaches from detached window back to embedded mode
    /// </summary>
    public void AttachToEmbedded()
    {
        Log.Information("Attaching to embedded view from detached window");

        // Update preference - user wants embedded mode
        PreferEmbedded = true;

        // Deactivate detached window
        DeactivateDetachedWindow();

        // Activate embedded view
        ActivateEmbeddedView();

        Log.Information("Successfully attached from window to embedded");
    }

    /// <summary>
    /// Loads student files into the shared ViewModel
    /// </summary>
    public async System.Threading.Tasks.Task LoadStudentFilesAsync(string studentName, string className, string studentFolderPath, Models.Students.Student? student = null)
    {
        Log.Information("Loading student files - Student: {StudentName}, Class: {ClassName}, Path: {Path}", 
            studentName, className, studentFolderPath);

        try
        {
            await SharedViewModel.LoadStudentFilesAsync(studentName, className, studentFolderPath, student);
            Log.Information("Student files loaded successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading student files");
            throw;
        }
    }

    /// <summary>
    /// Clears the current student data from the shared ViewModel
    /// </summary>
    public void ClearStudentData()
    {
        Log.Information("Clearing student data from shared ViewModel");
        
        // Create a fresh ViewModel instance
        _sharedViewModel = new StudentDetailViewModel();
        
        Log.Information("Student data cleared");
    }
}

