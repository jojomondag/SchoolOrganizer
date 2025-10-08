using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using SchoolOrganizer.Views.TestWindows;

namespace SchoolOrganizer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetWindowIcon();

        // Intercept window closing to hide instead
        Closing += OnWindowClosing;
        
        // Add keyboard shortcut for testing
        KeyDown += OnKeyDown;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // Cancel the close operation
        e.Cancel = true;

        // Hide the window instead
        Hide();
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

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+T to open StudentDetailView test window
        if (e.Key == Key.T && e.KeyModifiers == KeyModifiers.Control)
        {
            OpenTestWindow();
        }
    }

    private void OnTestStudentDetailClick(object? sender, RoutedEventArgs e)
    {
        OpenTestWindow();
    }

    private void OpenTestWindow()
    {
        var testWindow = new StudentDetailTestWindow();
        testWindow.Show();
    }
}