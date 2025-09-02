using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace SchoolOrganizer.Services;

public static class ProfileImageStore
{
    private static readonly object fileLock = new();

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
            var map = await ReadMapAsync();
            if (map.TryGetValue(studentId, out var path) && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return path;
            }
        }
        catch (Exception)
        {
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
}


