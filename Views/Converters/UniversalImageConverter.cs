using System;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net.Http;

namespace SchoolOrganizer.Views.Converters;

public class UniversalImageConverter : IValueConverter
{
    // Cache to track file modification times to force refresh when file changes
    private static readonly ConcurrentDictionary<string, (DateTime lastModified, Bitmap? bitmap)> _bitmapCache = new();
    private static readonly HttpClient _httpClient = new();
    private static readonly ConcurrentDictionary<string, Task<Bitmap?>> _loadingTasks = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            try
            {
                if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    // For HTTP/HTTPS URLs, check cache first
                    if (_bitmapCache.TryGetValue(path, out var cached) && cached.bitmap != null)
                    {
                        return cached.bitmap;
                    }
                    
                    // For now, return null to show placeholder - this eliminates the blocking network calls
                    // TODO: Implement proper async image loading with UI updates
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

    private static async Task<Bitmap?> LoadImageFromUrlAsync(string url)
    {
        try
        {
            // Check cache first
            if (_bitmapCache.TryGetValue(url, out var cached) && cached.bitmap != null)
            {
                return cached.bitmap;
            }

            // Download the image
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            using var stream = await response.Content.ReadAsStreamAsync();
            var bitmap = new Bitmap(stream);
            
            // Cache the result
            _bitmapCache.TryAdd(url, (DateTime.Now, bitmap));
            
            // Clean up loading task
            _loadingTasks.TryRemove(url, out _);
            
            return bitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading URL image: {url}, {ex.Message}");
            
            // Clean up loading task on error
            _loadingTasks.TryRemove(url, out _);
            return null;
        }
    }
}


