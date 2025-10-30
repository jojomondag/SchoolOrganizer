using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SchoolOrganizer.Src.ViewModels;
using SchoolOrganizer.Src.Services;
using Serilog;

namespace SchoolOrganizer.Src.Views.AssignmentManagement;

/// <summary>
/// Window for displaying a student's downloaded assignments in detached mode
/// </summary>
public partial class AssignmentViewer : Window
{
    private const string WindowName = "AssignmentViewer";
    private readonly SettingsService _settingsService;
    private DispatcherTimer? _saveDebounceTimer;
    private double _lastNormalX;
    private double _lastNormalY;
    private double _lastNormalWidth;
    private double _lastNormalHeight;
    private bool _hasLastNormalBounds;

    public AssignmentViewer()
    {
        InitializeComponent();
        _settingsService = SettingsService.Instance;
        
        // Subscribe to window events to save bounds
        PositionChanged += OnPositionChanged;
        PropertyChanged += OnWindowPropertyChanged;
        Closing += OnWindowClosing;
        
        // Load saved window bounds after window is opened
        Opened += OnWindowOpened;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        // Use a small delay to ensure window is fully initialized
        Avalonia.Threading.Dispatcher.UIThread.Post(() => LoadWindowBounds(), Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        Log.Information("=== CLOSING AssignmentViewer Window ===");
        Log.Information("Window values at close time - State: {State}, Pos: {X},{Y}, Size: {W}x{H}", 
            WindowState, Position.X, Position.Y, Width, Height);
        
        // Save final window state when closing
        SaveWindowBounds();
        
        Log.Information("=== AssignmentViewer window bounds saved on close ===");
    }

    public AssignmentViewer(StudentDetailViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void LoadWindowBounds()
    {
        try
        {
            Log.Information("=== LOADING AssignmentViewer Window Bounds ===");
            Log.Information("Current state BEFORE load - State: {State}, Pos: {X},{Y}, Size: {W}x{H}", 
                WindowState, Position.X, Position.Y, Width, Height);
            
            var bounds = _settingsService.LoadWindowBounds(WindowName);
            if (bounds != null)
            {
                Log.Information("Loaded bounds from settings - State: {State}, Pos: {X},{Y}, Size: {W}x{H}", 
                    bounds.WindowState, bounds.X, bounds.Y, bounds.Width, bounds.Height);

                // Always set position first so maximized goes to intended screen
                Position = new Avalonia.PixelPoint((int)bounds.X, (int)bounds.Y);

                // If normal, also set size
                if ((WindowState)bounds.WindowState == WindowState.Normal)
                {
                    Width = bounds.Width;
                    Height = bounds.Height;
                    
                    // Ensure the window is on a visible screen
                    try
                    {
                        var screens = Screens;
                        var screenAtPos = screens?.ScreenFromPoint(Position);
                        if (screenAtPos == null)
                        {
                            var primary = screens?.Primary;
                            if (primary != null)
                            {
                                var wa = primary.WorkingArea;
                                var newX = wa.X + 10;
                                var newY = wa.Y + 10;
                                Position = new Avalonia.PixelPoint(newX, newY);
                                Log.Information("Saved position was off-screen. Moved AssignmentViewer to primary screen at: {X},{Y}", newX, newY);
                            }
                        }
                    }
                    catch { }
                    Log.Information("Set position to: {X},{Y} and size to: {W}x{H}", bounds.X, bounds.Y, bounds.Width, bounds.Height);
                }
                else
                {
                    Log.Information("WindowState is not Normal, skipping position/size restore");
                }

                // Finally restore window state
                WindowState = (WindowState)bounds.WindowState;
                Log.Information("Set WindowState to: {State}", WindowState);
                
                Log.Information("=== LOADED AssignmentViewer Window Bounds - Final State: {State}, Pos: {X},{Y}, Size: {W}x{H} ===", 
                    WindowState, Position.X, Position.Y, Width, Height);
            }
            else
            {
                Log.Warning("No saved bounds found for AssignmentViewer");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading AssignmentViewer window bounds");
        }
    }

    private void OnPositionChanged(object? sender, EventArgs e)
    {
        Log.Information("AssignmentViewer Position changed to: {X},{Y}", Position.X, Position.Y);
        if (WindowState == WindowState.Normal)
        {
            _lastNormalX = Position.X;
            _lastNormalY = Position.Y;
            _lastNormalWidth = Width;
            _lastNormalHeight = Height;
            _hasLastNormalBounds = true;
        }
        DebouncedSaveWindowBounds();
    }

    private void OnWindowPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WidthProperty)
        {
            Log.Information("AssignmentViewer Width changed to: {Width}", Width);
            if (WindowState == WindowState.Normal)
            {
                _lastNormalWidth = Width;
                _hasLastNormalBounds = true;
            }
            DebouncedSaveWindowBounds();
        }
        else if (e.Property == HeightProperty)
        {
            Log.Information("AssignmentViewer Height changed to: {Height}", Height);
            if (WindowState == WindowState.Normal)
            {
                _lastNormalHeight = Height;
                _hasLastNormalBounds = true;
            }
            DebouncedSaveWindowBounds();
        }
        else if (e.Property == WindowStateProperty)
        {
            Log.Information("AssignmentViewer WindowState changed to: {State}", WindowState);
            DebouncedSaveWindowBounds();
        }
    }

    private void DebouncedSaveWindowBounds()
    {
        _saveDebounceTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _saveDebounceTimer.Tick -= OnSaveDebounceTick;
        _saveDebounceTimer.Tick += OnSaveDebounceTick;
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
    }

    private void OnSaveDebounceTick(object? sender, EventArgs e)
    {
        _saveDebounceTimer?.Stop();
        SaveWindowBounds();
    }

    private void SaveWindowBounds()
    {
        try
        {
            Log.Information("=== SAVING AssignmentViewer Window Bounds ===");
            Log.Information("Current window values - State: {State}, Pos: {X},{Y}, Size: {W}x{H}", 
                WindowState, Position.X, Position.Y, Width, Height);
            
            double x, y, width, height;
            
            if (WindowState == WindowState.Normal)
            {
                // Save current position and size when in normal state
                x = Position.X;
                y = Position.Y;
                width = Width;
                height = Height;
                Log.Information("Window is Normal - saving current values");
                _lastNormalX = x;
                _lastNormalY = y;
                _lastNormalWidth = width;
                _lastNormalHeight = height;
                _hasLastNormalBounds = true;
            }
            else
            {
                // When maximized/fullscreen, preserve the previous normal position/size from memory
                if (_hasLastNormalBounds)
                {
                    x = _lastNormalX;
                    y = _lastNormalY;
                    width = _lastNormalWidth;
                    height = _lastNormalHeight;
                    Log.Information("Window is {State} - preserving last known Normal bounds from memory", WindowState);
                }
                else
                {
                    // No in-memory normal bounds: anchor to the current screen's working area
                    var screen = Screens?.ScreenFromWindow(this) ?? Screens?.ScreenFromPoint(Position);
                    if (screen != null)
                    {
                        var wa = screen.WorkingArea;
                        x = wa.X + 10;
                        y = wa.Y + 10;
                        width = Math.Min(Width, wa.Width - 20);
                        height = Math.Min(Height, wa.Height - 20);
                        Log.Information("Window is {State} - anchoring normal bounds to current screen working area {X},{Y}", WindowState, x, y);
                    }
                    else
                    {
                        x = Position.X;
                        y = Position.Y;
                        width = Width;
                        height = Height;
                        Log.Warning("No screen found - using current values for saved normal bounds");
                    }
                }
            }
            
            // Always save the current window state
            _settingsService.SaveWindowBounds(WindowName, x, y, width, height, (int)WindowState);
            
            Log.Information("=== SAVED AssignmentViewer bounds - State: {State}, Pos: {X},{Y}, Size: {W}x{H} ===", 
                WindowState, x, y, width, height);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving AssignmentViewer window bounds");
        }
    }

    /// <summary>
    /// Handles the attach button click - moves the view back to embedded mode
    /// </summary>
    private void OnAttachClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Attach button clicked in detached window");
            
            // Use the coordinator to handle the attach operation
            AssignmentViewCoordinator.Instance.AttachToEmbedded();
            
            Log.Information("Successfully initiated attach to embedded view");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error attaching assignment view");
        }
    }
}
