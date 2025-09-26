using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace SchoolOrganizer.Services;

public class CropSettings
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double RotationAngle { get; set; }
    public double ImageDisplayWidth { get; set; }
    public double ImageDisplayHeight { get; set; }
    public double ImageDisplayOffsetX { get; set; }
    public double ImageDisplayOffsetY { get; set; }
}

public static class ProfileImageStore
{

    private static string GetImagesDir()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var imagesDir = Path.Combine(baseDir, "Data", "ProfileImages");
        if (!Directory.Exists(imagesDir)) Directory.CreateDirectory(imagesDir);
        return imagesDir;
    }

    private static string GetOriginalsDir()
    {
        var imagesDir = GetImagesDir();
        var originalsDir = Path.Combine(imagesDir, "Originals");
        if (!Directory.Exists(originalsDir)) Directory.CreateDirectory(originalsDir);
        return originalsDir;
    }

    private static string GetMapFile()
    {
        var imagesDir = GetImagesDir();
        return Path.Combine(imagesDir, "profile_sources.json");
    }

    private static string GetCropSettingsFile()
    {
        var imagesDir = GetImagesDir();
        return Path.Combine(imagesDir, "crop_settings.json");
    }

    public static async Task<string> SaveOriginalFromLocalPathAsync(string sourcePath)
    {
        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
        var dest = Path.Combine(GetOriginalsDir(), $"orig_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}{ext}");
        using (var src = File.OpenRead(sourcePath))
        using (var dst = File.Create(dest))
        {
            await src.CopyToAsync(dst);
        }
        return dest;
    }

    public static async Task<string> SaveOriginalFromStorageFileAsync(IStorageFile file)
    {
        var ext = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
        var dest = Path.Combine(GetOriginalsDir(), $"orig_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}{ext}");
        await using (var src = await file.OpenReadAsync())
        await using (var dst = File.Create(dest))
        {
            await src.CopyToAsync(dst);
        }
        return dest;
    }

    public static async Task MapStudentToOriginalAsync(int studentId, string storedOriginalPath)
    {
        try
        {
            var map = await ReadMapAsync();
            map[studentId] = storedOriginalPath;
            await WriteMapAsync(map);
        }
        catch (Exception)
        {
            // ignore mapping failures
        }
    }

    public static async Task<string?> GetOriginalForStudentAsync(int studentId)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"GetOriginalForStudentAsync: Looking for student {studentId}");
            var map = await ReadMapAsync();
            System.Diagnostics.Debug.WriteLine($"GetOriginalForStudentAsync: Map contains {map.Count} entries");
            
            if (map.TryGetValue(studentId, out var path) && !string.IsNullOrWhiteSpace(path))
            {
                bool exists = File.Exists(path);
                System.Diagnostics.Debug.WriteLine($"GetOriginalForStudentAsync: Found path {path}, exists: {exists}");
                if (exists)
                {
                    return path;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"GetOriginalForStudentAsync: No mapping found for student {studentId}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetOriginalForStudentAsync error: {ex.Message}");
        }
        return null;
    }

    public static async Task SaveCropSettingsForStudentAsync(int studentId, CropSettings settings)
    {
        try
        {
            var cropMap = await ReadCropSettingsMapAsync();
            cropMap[studentId] = settings;
            await WriteCropSettingsMapAsync(cropMap);
        }
        catch (Exception)
        {
            // ignore save failures
        }
    }

    public static async Task<CropSettings?> GetCropSettingsForStudentAsync(int studentId)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"GetCropSettingsForStudentAsync: Looking for student {studentId}");
            var cropMap = await ReadCropSettingsMapAsync();
            System.Diagnostics.Debug.WriteLine($"GetCropSettingsForStudentAsync: Crop map contains {cropMap.Count} entries");
            
            if (cropMap.TryGetValue(studentId, out var settings))
            {
                System.Diagnostics.Debug.WriteLine($"GetCropSettingsForStudentAsync: Found settings - X:{settings.X}, Y:{settings.Y}, W:{settings.Width}, H:{settings.Height}");
                return settings;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"GetCropSettingsForStudentAsync: No crop settings found for student {studentId}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetCropSettingsForStudentAsync error: {ex.Message}");
        }
        return null;
    }

    private static async Task<Dictionary<int, string>> ReadMapAsync()
    {
        var file = GetMapFile();
        if (!File.Exists(file)) return new Dictionary<int, string>();
        try
        {
            using var stream = File.OpenRead(file);
            var map = await JsonSerializer.DeserializeAsync<Dictionary<int, string>>(stream);
            return map ?? new Dictionary<int, string>();
        }
        catch
        {
            return new Dictionary<int, string>();
        }
    }

    private static async Task WriteMapAsync(Dictionary<int, string> map)
    {
        var file = GetMapFile();
        await using var stream = File.Create(file);
        var options = new JsonSerializerOptions { WriteIndented = true };
        await JsonSerializer.SerializeAsync(stream, map, options);
    }

    private static async Task<Dictionary<int, CropSettings>> ReadCropSettingsMapAsync()
    {
        var file = GetCropSettingsFile();
        if (!File.Exists(file)) return new Dictionary<int, CropSettings>();
        try
        {
            using var stream = File.OpenRead(file);
            var map = await JsonSerializer.DeserializeAsync<Dictionary<int, CropSettings>>(stream);
            return map ?? new Dictionary<int, CropSettings>();
        }
        catch
        {
            return new Dictionary<int, CropSettings>();
        }
    }

    private static async Task WriteCropSettingsMapAsync(Dictionary<int, CropSettings> map)
    {
        var file = GetCropSettingsFile();
        await using var stream = File.Create(file);
        var options = new JsonSerializerOptions { WriteIndented = true };
        await JsonSerializer.SerializeAsync(stream, map, options);
    }
}


