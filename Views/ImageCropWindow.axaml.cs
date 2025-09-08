using Avalonia.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using ImageSelector;
using SchoolOrganizer.Services;
using System.Linq;

namespace SchoolOrganizer.Views;

public partial class ImageCropWindow : Window
{
    public string? SavedImagePath { get; private set; }
    private int? studentContextId;
    private string? preloadedOriginalPath;

    public ImageCropWindow()
    {
        InitializeComponent();
        if (this.Content is ImageSelectorView selector)
        {
            selector.SavePathProvider = PrepareSavePath;
            selector.AvailableImagesProvider = GetAvailableImages;
            selector.CropSettingsProvider = GetCropSettingsForImage;
            
            // Allow auto-resize only on initial load, then disable it to prevent resizing when switching gallery images
            selector.ImageSaved += async (s, path) =>
            {
                SavedImagePath = path;
                System.Diagnostics.Debug.WriteLine($"ImageCropWindow: Image saved to {path}");
                
                // Save crop settings if we have a student context
                if (studentContextId is int id)
                {
                    var cropSettings = selector.GetCurrentCropSettings();
                    if (cropSettings != null)
                    {
                        // Convert anonymous object to CropSettings
                        var settingsType = cropSettings.GetType();
                        var newSettings = new CropSettings
                        {
                            X = Convert.ToDouble(settingsType.GetProperty("X")?.GetValue(cropSettings) ?? 0),
                            Y = Convert.ToDouble(settingsType.GetProperty("Y")?.GetValue(cropSettings) ?? 0),
                            Width = Convert.ToDouble(settingsType.GetProperty("Width")?.GetValue(cropSettings) ?? 0),
                            Height = Convert.ToDouble(settingsType.GetProperty("Height")?.GetValue(cropSettings) ?? 0),
                            RotationAngle = Convert.ToDouble(settingsType.GetProperty("RotationAngle")?.GetValue(cropSettings) ?? 0),
                            ImageDisplayWidth = Convert.ToDouble(settingsType.GetProperty("ImageDisplayWidth")?.GetValue(cropSettings) ?? 0),
                            ImageDisplayHeight = Convert.ToDouble(settingsType.GetProperty("ImageDisplayHeight")?.GetValue(cropSettings) ?? 0),
                            ImageDisplayOffsetX = Convert.ToDouble(settingsType.GetProperty("ImageDisplayOffsetX")?.GetValue(cropSettings) ?? 0),
                            ImageDisplayOffsetY = Convert.ToDouble(settingsType.GetProperty("ImageDisplayOffsetY")?.GetValue(cropSettings) ?? 0)
                        };
                        
                        System.Diagnostics.Debug.WriteLine($"Saving crop settings for student {id}: X:{newSettings.X}, Y:{newSettings.Y}, W:{newSettings.Width}, H:{newSettings.Height}");
                        await ProfileImageStore.SaveCropSettingsForStudentAsync(id, newSettings);
                        System.Diagnostics.Debug.WriteLine("Crop settings saved successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"No crop settings to save for student {id}");
                    }
                }
                
                Close();
            };
            // Persist latest crop on close as well, so next open restores position even without saving image
            this.Closed += async (s, e) =>
            {
                try
                {
                    if (studentContextId is int id)
                    {
                        var cropSettings = selector.GetCurrentCropSettings();
                        if (cropSettings != null)
                        {
                            var settingsType = cropSettings.GetType();
                            var newSettings = new CropSettings
                            {
                                X = Convert.ToDouble(settingsType.GetProperty("X")?.GetValue(cropSettings) ?? 0),
                                Y = Convert.ToDouble(settingsType.GetProperty("Y")?.GetValue(cropSettings) ?? 0),
                                Width = Convert.ToDouble(settingsType.GetProperty("Width")?.GetValue(cropSettings) ?? 0),
                                Height = Convert.ToDouble(settingsType.GetProperty("Height")?.GetValue(cropSettings) ?? 0),
                                RotationAngle = Convert.ToDouble(settingsType.GetProperty("RotationAngle")?.GetValue(cropSettings) ?? 0),
                                ImageDisplayWidth = Convert.ToDouble(settingsType.GetProperty("ImageDisplayWidth")?.GetValue(cropSettings) ?? 0),
                                ImageDisplayHeight = Convert.ToDouble(settingsType.GetProperty("ImageDisplayHeight")?.GetValue(cropSettings) ?? 0),
                                ImageDisplayOffsetX = Convert.ToDouble(settingsType.GetProperty("ImageDisplayOffsetX")?.GetValue(cropSettings) ?? 0),
                                ImageDisplayOffsetY = Convert.ToDouble(settingsType.GetProperty("ImageDisplayOffsetY")?.GetValue(cropSettings) ?? 0)
                            };
                            await ProfileImageStore.SaveCropSettingsForStudentAsync(id, newSettings);
                        }
                    }
                }
                catch
                {
                }
            };
            selector.OriginalImageSelected += async (s, file) =>
            {
                try
                {
                    // Disable auto-resize after first image selection to prevent window resizing when switching gallery images
                    selector.AutoResizeWindow = false;
                    
                    // Persist a copy of the original and map to student (if known)
                    var stored = await ProfileImageStore.SaveOriginalFromStorageFileAsync(file);
                    if (studentContextId is int sid)
                    {
                        await ProfileImageStore.MapStudentToOriginalAsync(sid, stored);
                        preloadedOriginalPath = stored;
                        
                        // After storing the original, try to load existing crop settings for this student
                        var existingCropSettings = await ProfileImageStore.GetCropSettingsForStudentAsync(sid);
                        if (existingCropSettings != null)
                        {
                            // Apply the saved crop settings to the newly loaded image
                            await Task.Delay(200); // Give the image time to load and layout
                            await selector.LoadImageFromPathWithCropSettingsAsync(stored, existingCropSettings);
                        }
                    }
                    
                    // Refresh the gallery to show the newly added image
                    await selector.RefreshImageGalleryAsync();
                }
                catch
                {
                    // ignore errors persisting original
                }
            };
        }
    }

    public static async Task<string?> ShowAsync(Window parent)
    {
        var dialog = new ImageCropWindow();
        await dialog.ShowDialog(parent);
        return dialog.SavedImagePath;
    }

    public static async Task<string?> ShowForStudentAsync(Window parent, int studentId)
    {
        var dialog = new ImageCropWindow();
        dialog.studentContextId = studentId;
        await dialog.TryPreloadOriginalAsync();
        await dialog.ShowDialog(parent);
        return dialog.SavedImagePath;
    }

    private Task<string?> PrepareSavePath()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var imagesDir = Path.Combine(baseDir, "Data", "ProfileImages");
            if (!Directory.Exists(imagesDir))
            {
                Directory.CreateDirectory(imagesDir);
            }

            // Generate unique filename
            var fileName = $"student_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png";
            var path = Path.Combine(imagesDir, fileName);
            return Task.FromResult<string?>(path);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    private async Task TryPreloadOriginalAsync()
    {
        if (this.Content is not ImageSelectorView selector) return;
        if (studentContextId is int id)
        {
            System.Diagnostics.Debug.WriteLine($"TryPreloadOriginalAsync: Loading for student {id}");
            
            var prior = await ProfileImageStore.GetOriginalForStudentAsync(id);
            if (!string.IsNullOrWhiteSpace(prior) && System.IO.File.Exists(prior))
            {
                System.Diagnostics.Debug.WriteLine($"TryPreloadOriginalAsync: Found original image at {prior}");
                preloadedOriginalPath = prior;
                
                // Try to load saved crop settings
                var cropSettings = await ProfileImageStore.GetCropSettingsForStudentAsync(id);
                if (cropSettings != null)
                {
                    System.Diagnostics.Debug.WriteLine($"TryPreloadOriginalAsync: Found crop settings - X:{cropSettings.X}, Y:{cropSettings.Y}, W:{cropSettings.Width}, H:{cropSettings.Height}");
                    await selector.LoadImageFromPathWithCropSettingsAsync(prior, cropSettings);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("TryPreloadOriginalAsync: No crop settings found, using defaults");
                    await selector.LoadImageFromPathAsync(prior);
                }
                
                // Disable auto-resize after initial load to prevent resizing when switching gallery images
                selector.AutoResizeWindow = false;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("TryPreloadOriginalAsync: No original image found");
            }
        }
    }

    private async Task<object?> GetCropSettingsForImage(string imagePath)
    {
        try
        {
            // Only provide crop settings if we have a student context
            if (studentContextId is int studentId)
            {
                return await ProfileImageStore.GetCropSettingsForStudentAsync(studentId);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private Task<string[]> GetAvailableImages()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var originalsDir = Path.Combine(baseDir, "Data", "ProfileImages", "Originals");
            
            if (!Directory.Exists(originalsDir))
            {
                return Task.FromResult(Array.Empty<string>());
            }

            var supportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            var imageFiles = Directory.GetFiles(originalsDir)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .OrderByDescending(File.GetLastWriteTime) // Most recent first
                .ToArray();

            return Task.FromResult(imageFiles);
        }
        catch
        {
            return Task.FromResult(Array.Empty<string>());
        }
    }
}


