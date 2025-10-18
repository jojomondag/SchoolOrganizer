using System;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System.Globalization;
using System.IO;
using System.Collections.Concurrent;

namespace SchoolOrganizer.Src.Converters;

public class UniversalImageConverter : IValueConverter
{
    // Cache to track file modification times to force refresh when file changes
    private static readonly ConcurrentDictionary<string, (DateTime lastModified, Bitmap? bitmap)> _bitmapCache = new();
    
    // Shared instance to ensure all cards use the same cache
    public static readonly UniversalImageConverter SharedInstance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            try
            {
                // Skip loading for special values that aren't real file paths
                if (path == "ADD_CARD" || path == "" || path == "null")
                {
                    return null;
                }

                if (File.Exists(path))
                {
                    // Check if we have a cached bitmap and if the file has been modified
                    var fileInfo = new FileInfo(path);
                    var lastModified = fileInfo.LastWriteTime;
                    
                    if (_bitmapCache.TryGetValue(path, out var cached) && 
                        cached.lastModified == lastModified && 
                        cached.bitmap != null)
                    {
                        return cached.bitmap;
                    }

                    // Try reading with shared access and a few quick retries in case the file
                    // is still being finalized by the image saver.
                    const int maxAttempts = 1; // Reduced to 1 attempt for better performance
                    for (int attempt = 0; attempt < maxAttempts; attempt++)
                    {
                        try
                        {
                            // Use File.ReadAllBytes for better performance
                            var imageBytes = File.ReadAllBytes(path);
                            using var memoryStream = new MemoryStream(imageBytes);
                            var originalBitmap = new Bitmap(memoryStream);
                            
                            // Downscale large images to improve performance and reduce memory usage
                            const int maxDimension = 800; // Maximum width or height
                            var bitmap = originalBitmap;
                            
                            if (originalBitmap.PixelSize.Width > maxDimension || originalBitmap.PixelSize.Height > maxDimension)
                            {
                                var scale = Math.Min((double)maxDimension / originalBitmap.PixelSize.Width, 
                                                   (double)maxDimension / originalBitmap.PixelSize.Height);
                                
                                var newWidth = (int)(originalBitmap.PixelSize.Width * scale);
                                var newHeight = (int)(originalBitmap.PixelSize.Height * scale);
                                
                                // Create downscaled bitmap
                                bitmap = originalBitmap.CreateScaledBitmap(new Avalonia.PixelSize(newWidth, newHeight), 
                                    Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);
                                
                                // Dispose original to free memory
                                originalBitmap.Dispose();
                            }
                            
                            // Update cache
                            _bitmapCache.AddOrUpdate(path, 
                                (lastModified, bitmap), 
                                (key, oldValue) => (lastModified, bitmap));
                            
                            return bitmap;
                        }
                        catch
                        {
                            if (attempt == maxAttempts - 1) throw;
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value;
    
    // Method to clear cache for a specific path (useful when we know an image has been updated)
    public static void ClearCache(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            System.Diagnostics.Debug.WriteLine($"UniversalImageConverter.ClearCache called for: {path}");
            var removed = _bitmapCache.TryRemove(path, out _);
            System.Diagnostics.Debug.WriteLine($"Cache entry removed: {removed}");
        }
    }
}
