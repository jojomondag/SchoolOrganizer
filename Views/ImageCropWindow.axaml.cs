using Avalonia.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using ImageSelector;

namespace SchoolOrganizer.Views;

public partial class ImageCropWindow : Window
{
    public string? SavedImagePath { get; private set; }

    public ImageCropWindow()
    {
        InitializeComponent();
        if (this.Content is ImageSelectorView selector)
        {
            selector.SavePathProvider = PrepareSavePath;
            selector.ImageSaved += (s, path) =>
            {
                SavedImagePath = path;
                Close();
            };
        }
    }

    public static async Task<string?> ShowAsync(Window parent)
    {
        var dialog = new ImageCropWindow();
        await dialog.ShowDialog(parent);
        return dialog.SavedImagePath;
    }

    private async Task<string?> PrepareSavePath()
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
            return path;
        }
        catch
        {
            return null;
        }
    }
}


