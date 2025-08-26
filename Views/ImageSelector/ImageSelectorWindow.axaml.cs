using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using SchoolOrganizer.ViewModels;

namespace SchoolOrganizer.Views.ImageSelector;

public partial class ImageSelectorWindow : Window
{
    public ImageSelectorViewModel ViewModel { get; }
    public string? SelectedImagePath { get; private set; }

    public ImageSelectorWindow()
    {
        InitializeComponent();
        ViewModel = new ImageSelectorViewModel();
        DataContext = ViewModel;

        // Subscribe to events
        ViewModel.ImageSelected += OnImageSelected;
        ViewModel.BrowseRequested += OnBrowseRequested;
    }

    private void OnImageSelected(object? sender, string imagePath)
    {
        SelectedImagePath = imagePath;
        Close();
    }

    private async void OnBrowseRequested(object? sender, EventArgs e)
    {
        await HandleBrowseCommand();
    }

    private async Task HandleBrowseCommand()
    {
        try
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Profile Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.webp" },
                        MimeTypes = new[] { "image/*" }
                    },
                    new FilePickerFileType("JPEG Images")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg" },
                        MimeTypes = new[] { "image/jpeg" }
                    },
                    new FilePickerFileType("PNG Images")
                    {
                        Patterns = new[] { "*.png" },
                        MimeTypes = new[] { "image/png" }
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            });

            if (files.Count > 0)
            {
                var selectedFile = files[0];
                var filePath = selectedFile.Path.LocalPath;

                // Add the image to the gallery and select it
                await ViewModel.AddImageToGallery(filePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in browse command: {ex.Message}");
        }
    }

    public static async Task<string?> ShowAsync(Window parent)
    {
        var dialog = new ImageSelectorWindow();
        
        await dialog.ShowDialog(parent);
        
        return dialog.SelectedImagePath;
    }

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from events to prevent memory leaks
        ViewModel.ImageSelected -= OnImageSelected;
        ViewModel.BrowseRequested -= OnBrowseRequested;
        base.OnClosed(e);
    }
}
