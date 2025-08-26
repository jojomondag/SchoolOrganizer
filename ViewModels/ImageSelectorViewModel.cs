using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SchoolOrganizer.ViewModels;

public partial class ImageSelectorViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<string> availableImages = new();

    [ObservableProperty]
    private string? selectedImagePath;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string currentImageFolder = string.Empty;

    [ObservableProperty]
    private string? previewImagePath;

    // Event to notify when an image is selected
    public event EventHandler<string>? ImageSelected;

    // Event to notify when browse is requested (to be handled by the window)
    public event EventHandler? BrowseRequested;

    public ImageSelectorViewModel()
    {
        _ = LoadDefaultImages();
    }

    [RelayCommand]
    private async Task LoadDefaultImages()
    {
        IsLoading = true;
        try
        {
            // Load default images from the Data folder
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "ProfileImages");
            CurrentImageFolder = dataPath;

            // Create the directory if it doesn't exist
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            await LoadImagesFromFolder(dataPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading default images: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void BrowseForImage()
    {
        try
        {
            // Notify the window to handle the file dialog
            BrowseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error browsing for image: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SelectImage(string? imagePath)
    {
        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            SelectedImagePath = imagePath;
            PreviewImagePath = imagePath;
        }
    }

    [RelayCommand]
    private void ConfirmSelection()
    {
        if (!string.IsNullOrEmpty(SelectedImagePath))
        {
            ImageSelected?.Invoke(this, SelectedImagePath);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        ImageSelected?.Invoke(this, string.Empty);
    }

    [RelayCommand]
    private async Task RemoveImage(string? imagePath)
    {
        try
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return;

            // Don't allow removing files outside our profile images folder
            if (!imagePath.StartsWith(CurrentImageFolder, StringComparison.OrdinalIgnoreCase))
                return;

            // Delete the file
            File.Delete(imagePath);

            // If this was the selected image, clear the selection
            if (SelectedImagePath == imagePath)
            {
                SelectedImagePath = null;
            }

            // Refresh the image list
            await LoadImagesFromFolder(CurrentImageFolder);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error removing image: {ex.Message}");
        }
    }

    private async Task LoadImagesFromFolder(string folderPath)
    {
        await Task.Run(() =>
        {
            try
            {
                AvailableImages.Clear();

                if (Directory.Exists(folderPath))
                {
                    var supportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
                    var imageFiles = Directory.GetFiles(folderPath)
                        .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .OrderBy(f => Path.GetFileName(f))
                        .ToList();

                    foreach (var imagePath in imageFiles)
                    {
                        System.Diagnostics.Debug.WriteLine($"Found image: {imagePath}");
                        AvailableImages.Add(imagePath);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Total images loaded: {AvailableImages.Count}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading images from folder: {ex.Message}");
            }
        });
    }

    public async Task AddImageToGallery(string sourcePath)
    {
        try
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                return;

            var fileName = Path.GetFileName(sourcePath);
            var destinationPath = Path.Combine(CurrentImageFolder, fileName);

            // If file already exists, generate a unique name
            if (File.Exists(destinationPath))
            {
                var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                var counter = 1;

                do
                {
                    fileName = $"{nameWithoutExtension}_{counter}{extension}";
                    destinationPath = Path.Combine(CurrentImageFolder, fileName);
                    counter++;
                } while (File.Exists(destinationPath));
            }

            // Copy the file to the profile images folder
            File.Copy(sourcePath, destinationPath);

            // Refresh the image list
            await LoadImagesFromFolder(CurrentImageFolder);

            // Select the newly added image
            SelectedImagePath = destinationPath;
            PreviewImagePath = destinationPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding image to gallery: {ex.Message}");
        }
    }
}
