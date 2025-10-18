using System;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System.Globalization;
using System.IO;
using System.Collections.Concurrent;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Avalonia;
using Avalonia.Media;
using Serilog;

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

                            // Load bitmap with correct EXIF orientation
                            var originalBitmap = LoadBitmapWithCorrectOrientation(memoryStream, path);

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

    private static Bitmap LoadBitmapWithCorrectOrientation(Stream stream, string? filePath)
    {
        // Read EXIF orientation if available
        int orientation = 1; // Default: no rotation needed

        // Try reading EXIF from file path first
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(filePath);

                var exifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

                if (exifSubIfdDirectory != null)
                {
                    if (exifSubIfdDirectory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientationValue))
                    {
                        orientation = orientationValue;
                        Log.Debug("EXIF Orientation detected: {Orientation} from file: {FilePath}", orientation, filePath);
                    }
                }
                else
                {
                    var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();

                    if (exifIfd0Directory != null)
                    {
                        if (exifIfd0Directory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientationValue))
                        {
                            orientation = orientationValue;
                            Log.Debug("EXIF Orientation detected in IFD0: {Orientation}", orientation);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to read EXIF from file path: {FilePath}", filePath);
            }
        }

        // Load the bitmap
        var originalBitmap = new Bitmap(stream);

        // Apply rotation based on EXIF orientation
        switch (orientation)
        {
            case 1: // Normal - no rotation needed
                return originalBitmap;

            case 3: // Rotated 180°
                return RotateBitmap(originalBitmap, 180);

            case 6: // Rotated 90° CW (most common for portrait photos)
                return RotateBitmap(originalBitmap, 90);

            case 8: // Rotated 90° CCW
                return RotateBitmap(originalBitmap, 270);

            // Cases 2, 4, 5, 7 involve mirroring which is less common for photos
            default:
                return originalBitmap;
        }
    }

    private static Bitmap RotateBitmap(Bitmap source, int degrees)
    {
        if (degrees % 360 == 0)
            return source;

        // Calculate new dimensions based on rotation
        int newWidth, newHeight;
        if (degrees == 90 || degrees == 270)
        {
            // Swap dimensions for 90° and 270° rotations
            newWidth = source.PixelSize.Height;
            newHeight = source.PixelSize.Width;
        }
        else
        {
            newWidth = source.PixelSize.Width;
            newHeight = source.PixelSize.Height;
        }

        // Create a new bitmap with rotated dimensions
        var rotated = new RenderTargetBitmap(new PixelSize(newWidth, newHeight));

        using (var context = rotated.CreateDrawingContext())
        {
            // Apply rotation transformation
            var matrix = Matrix.Identity;

            switch (degrees)
            {
                case 90:
                    matrix = Matrix.CreateTranslation(-source.PixelSize.Width / 2.0, -source.PixelSize.Height / 2.0) *
                             Matrix.CreateRotation(Math.PI / 2) *
                             Matrix.CreateTranslation(newWidth / 2.0, newHeight / 2.0);
                    break;
                case 180:
                    matrix = Matrix.CreateTranslation(-source.PixelSize.Width / 2.0, -source.PixelSize.Height / 2.0) *
                             Matrix.CreateRotation(Math.PI) *
                             Matrix.CreateTranslation(newWidth / 2.0, newHeight / 2.0);
                    break;
                case 270:
                    matrix = Matrix.CreateTranslation(-source.PixelSize.Width / 2.0, -source.PixelSize.Height / 2.0) *
                             Matrix.CreateRotation(3 * Math.PI / 2) *
                             Matrix.CreateTranslation(newWidth / 2.0, newHeight / 2.0);
                    break;
            }

            using (context.PushTransform(matrix))
            {
                context.DrawImage(source,
                    new Rect(0, 0, source.PixelSize.Width, source.PixelSize.Height),
                    new Rect(0, 0, source.PixelSize.Width, source.PixelSize.Height));
            }
        }

        // Dispose the original bitmap
        source.Dispose();

        return rotated;
    }
}
