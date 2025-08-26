using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace SchoolOrganizer.Views;

public class UniversalImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"UniversalImageConverter: Processing path: {path}");
                
                // Check if it's a web URL
                if (path.StartsWith("http://") || path.StartsWith("https://"))
                {
                    System.Diagnostics.Debug.WriteLine("Loading web URL...");
                    // For web URLs, return the string as-is (Avalonia can handle these directly)
                    return path;
                }
                
                // Check if it's a local file path
                if (File.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine($"Loading local file: {path}");
                    
                    // Read the file into memory to avoid file locking
                    var bytes = File.ReadAllBytes(path);
                    using var stream = new MemoryStream(bytes);
                    var bitmap = new Bitmap(stream);
                    
                    System.Diagnostics.Debug.WriteLine($"Successfully loaded local image: {path}");
                    return bitmap;
                }
                
                System.Diagnostics.Debug.WriteLine($"File doesn't exist: {path}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading image {path}: {ex.Message}");
                return null;
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"Invalid or empty path: {value}");
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
