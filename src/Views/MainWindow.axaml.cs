using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using SchoolOrganizer.Src.Services;
using Serilog;

namespace SchoolOrganizer.Src.Views;

public partial class MainWindow : Window
{
    private const string WindowName = "MainWindow";
    private readonly SettingsService _settingsService;
    private DispatcherTimer? _saveDebounceTimer;
    private double _lastNormalX;
    private double _lastNormalY;
    private double _lastNormalWidth;
    private double _lastNormalHeight;
    private bool _hasLastNormalBounds;

    public MainWindow()
    {
        InitializeComponent();
        _settingsService = SettingsService.Instance;
        
        SetWindowIcon();

        // Intercept window closing to hide instead
        Closing += OnWindowClosing;
        
        // Subscribe to window events to save bounds
        PositionChanged += OnPositionChanged;
        PropertyChanged += OnWindowPropertyChanged;
        
        // Load saved window bounds after window is opened
        Opened += OnWindowOpened;
        
        // Keyboard shortcuts removed - functionality moved to main StudentDetail window
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        // Use a small delay to ensure window is fully initialized
        Avalonia.Threading.Dispatcher.UIThread.Post(() => LoadWindowBounds(), Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void LoadWindowBounds()
    {
        try
        {
            var bounds = _settingsService.LoadWindowBounds(WindowName);
            if (bounds != null)
            {
                // Always set position first so maximized goes to intended screen
                Position = new Avalonia.PixelPoint((int)bounds.X, (int)bounds.Y);
                
                // Only restore size if not maximized/fullscreen
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
                                Log.Information("Saved position was off-screen. Moved MainWindow to primary screen at: {X},{Y}", newX, newY);
                            }
                        }
                    }
                    catch { }
                }
                
                // Finally restore window state
                WindowState = (WindowState)bounds.WindowState;
                
                Log.Information("Restored MainWindow bounds - State: {State}, Pos: {X},{Y}, Size: {W}x{H}", 
                    WindowState, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading MainWindow bounds");
        }
    }

    private void OnPositionChanged(object? sender, EventArgs e)
    {
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
            if (WindowState == WindowState.Normal)
            {
                _lastNormalWidth = Width;
                _hasLastNormalBounds = true;
            }
            DebouncedSaveWindowBounds();
        }
        else if (e.Property == HeightProperty)
        {
            if (WindowState == WindowState.Normal)
            {
                _lastNormalHeight = Height;
                _hasLastNormalBounds = true;
            }
            DebouncedSaveWindowBounds();
        }
        else if (e.Property == WindowStateProperty)
        {
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
            double x, y, width, height;
            
            if (WindowState == WindowState.Normal)
            {
                // Save current position and size when in normal state
                x = Position.X;
                y = Position.Y;
                width = Width;
                height = Height;
                _lastNormalX = x;
                _lastNormalY = y;
                _lastNormalWidth = width;
                _lastNormalHeight = height;
                _hasLastNormalBounds = true;
            }
            else
            {
                if (_hasLastNormalBounds)
                {
                    x = _lastNormalX;
                    y = _lastNormalY;
                    width = _lastNormalWidth;
                    height = _lastNormalHeight;
                }
                else
                {
                    // Anchor to current screen if no last-normal known
                    var screen = Screens?.ScreenFromWindow(this) ?? Screens?.ScreenFromPoint(Position);
                    if (screen != null)
                    {
                        var wa = screen.WorkingArea;
                        x = wa.X + 10;
                        y = wa.Y + 10;
                        width = Math.Min(Width, wa.Width - 20);
                        height = Math.Min(Height, wa.Height - 20);
                    }
                    else
                    {
                        // Fallback to existing saved bounds or current
                        var existingBounds = _settingsService.LoadWindowBounds(WindowName);
                        if (existingBounds != null)
                        {
                            x = existingBounds.X;
                            y = existingBounds.Y;
                            width = existingBounds.Width;
                            height = existingBounds.Height;
                        }
                        else
                        {
                            x = Position.X;
                            y = Position.Y;
                            width = Width;
                            height = Height;
                        }
                    }
                }
            }
            
            // Always save the current window state
            _settingsService.SaveWindowBounds(WindowName, x, y, width, height, (int)WindowState);
            
            Log.Information("Saved MainWindow bounds - State: {State}, Pos: {X},{Y}, Size: {W}x{H}", 
                WindowState, x, y, width, height);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving MainWindow bounds");
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // Save final window state when closing
        SaveWindowBounds();
        Log.Information("Saved MainWindow bounds on close");
        
#if !DEBUG
        // In Release mode: cancel close and hide window (minimize to tray)
        e.Cancel = true;
        Hide();
#endif
        // In Debug mode: allow normal close (e.Cancel remains false)
    }

    private void SetWindowIcon()
    {
        try
        {
            // Choose icon based on operating system
            var iconPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "avares://SchoolOrganizer/Assets/icon.ico"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? "avares://SchoolOrganizer/Assets/icon.icns"
                    : "avares://SchoolOrganizer/Assets/Logo.png";

            Icon = new WindowIcon(iconPath);
        }
        catch
        {
            // If icon loading fails, continue without an icon
            // This prevents the app from crashing due to icon issues
        }
    }

    // Test window functionality removed - explorer toggle now available in main StudentDetail window

}