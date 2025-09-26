using System;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net.Http;

namespace SchoolOrganizer.Views;

public class UniversalImageConverter : IValueConverter
{
    // Cache to track file modification times to force refresh when file changes
    private static readonly ConcurrentDictionary<string, (DateTime lastModified, Bitmap? bitmap)> _bitmapCache = new();
    private static readonly HttpClient _httpClient = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            try
            {
                if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    // For HTTP/HTTPS URLs, download the image asynchronously
                    return LoadImageFromUrlAsync(path);
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
                    const int maxAttempts = 4;
                    const int delayMs = 50;
                    for (int attempt = 0; attempt < maxAttempts; attempt++)
                    {
                        try
                        {
                            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            // Create a memory stream to ensure the file handle is released immediately
                            var memoryStream = new MemoryStream();
                            stream.CopyTo(memoryStream);
                            memoryStream.Position = 0;
                            
                            var bitmap = new Bitmap(memoryStream);
                            
                            // Update cache
                            _bitmapCache.AddOrUpdate(path, 
                                (lastModified, bitmap), 
                                (key, oldValue) => (lastModified, bitmap));
                            
                            return bitmap;
                        }
                        catch
                        {
                            if (attempt == maxAttempts - 1) throw;
                            Thread.Sleep(delayMs);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading image from {path}: {ex.Message}");
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
            _bitmapCache.TryRemove(path, out _);
        }
    }

    private static Bitmap? LoadImageFromUrlAsync(string url)
    {
        try
        {
            // Check cache first
            if (_bitmapCache.TryGetValue(url, out var cached) && cached.bitmap != null)
            {
                return cached.bitmap;
            }

            // For now, just return null for URLs to prevent the file path errors
            // This will show the default placeholder image instead of crashing
            System.Diagnostics.Debug.WriteLine($"Skipping URL image loading for: {url}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error with URL image: {url}, {ex.Message}");
            return null;
        }
    }
}


