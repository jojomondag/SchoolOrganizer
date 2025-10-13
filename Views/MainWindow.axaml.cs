using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Native;
using SchoolOrganizer.ViewModels;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SchoolOrganizer.Views;

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

    // Test window functionality removed - explorer toggle now available in main StudentDetail window

    private void StudentGallery_Click(object? sender, RoutedEventArgs e)
    {
        NavigateToStudentGallery();
    }

    private void ClassroomDownload_Click(object? sender, RoutedEventArgs e)
    {
        NavigateToClassroomDownload();
    }

    private void StudentGallery_Native_Click(object? sender, EventArgs e)
    {
        NavigateToStudentGallery();
    }

    private void ClassroomDownload_Native_Click(object? sender, EventArgs e)
    {
        NavigateToClassroomDownload();
    }

    private void NavigateToStudentGallery()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.NavigateToStudentGalleryCommand.Execute(null);
        }
    }

    private void NavigateToClassroomDownload()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.NavigateToClassroomDownloadCommand.Execute(null);
        }
    }
}