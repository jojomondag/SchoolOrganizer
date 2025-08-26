using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace SchoolOrganizer.Views.ImageSelector;

public class ImagePathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Loading image: {path}");
                
                // Create a new Bitmap from the file
                // We need to read the file into a memory stream to avoid file locking
                var bytes = File.ReadAllBytes(path);
                using var stream = new MemoryStream(bytes);
                var bitmap = new Bitmap(stream);
                
                System.Diagnostics.Debug.WriteLine($"Successfully loaded image: {path}");
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading image {path}: {ex.Message}");
                return null;
            }
        }
        System.Diagnostics.Debug.WriteLine($"Invalid path or file doesn't exist: {value}");
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
