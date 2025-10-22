using Avalonia.Controls;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SchoolOrganizer.Src.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetWindowIcon();

        // Intercept window closing to hide instead
        Closing += OnWindowClosing;
        
        // Keyboard shortcuts removed - functionality moved to main StudentDetail window
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
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