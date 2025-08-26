using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace SchoolOrganizer.Views.ImageSelector;

public class AsyncImagePathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"AsyncConverter: Loading image: {path}");
                
                // Try different approaches based on the file extension
                var extension = Path.GetExtension(path).ToLowerInvariant();
                
                if (extension == ".ico")
                {
                    // For ICO files, we might need special handling
                    System.Diagnostics.Debug.WriteLine("Loading ICO file...");
                }
                
                // Load the bitmap using file stream
                using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
                var bitmap = new Bitmap(fileStream);
                
                System.Diagnostics.Debug.WriteLine($"AsyncConverter: Successfully loaded image: {path}, Size: {bitmap.PixelSize}");
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AsyncConverter: Error loading image {path}: {ex.Message}");
                
                // Return a placeholder or null
                return null;
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"AsyncConverter: Invalid path or file doesn't exist: {value}");
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
