using Avalonia.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using ImageSelector;
using SchoolOrganizer.Services;

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
            selector.ImageSaved += (s, path) =>
            {
                SavedImagePath = path;
                System.Diagnostics.Debug.WriteLine($"ImageCropWindow: Image saved to {path}");
                Close();
            };
            selector.OriginalImageSelected += async (s, file) =>
            {
                try
                {
                    // Persist a copy of the original and map to student (if known)
                    var stored = await ProfileImageStore.SaveOriginalFromStorageFileAsync(file);
                    if (studentContextId is int sid)
                    {
                        await ProfileImageStore.MapStudentToOriginalAsync(sid, stored);
                        preloadedOriginalPath = stored;
                    }
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
            var prior = await ProfileImageStore.GetOriginalForStudentAsync(id);
            if (!string.IsNullOrWhiteSpace(prior) && System.IO.File.Exists(prior))
            {
                preloadedOriginalPath = prior;
                await selector.LoadImageFromPathAsync(prior);
            }
        }
    }
}


