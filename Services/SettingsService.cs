using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Serilog;

namespace SchoolOrganizer.Services
{
    /// <summary>
    /// Service for managing application settings and user preferences
    /// </summary>
    public class SettingsService
    {
        private static readonly string _settingsFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "settings.json");

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private static SettingsService? _instance;
        public static SettingsService Instance => _instance ??= new SettingsService();

        private SettingsService()
        {
            // Ensure the settings file directory exists (app directory)
            var settingsDir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(settingsDir) && !Directory.Exists(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }
        }

        /// <summary>
        /// Saves the selected download folder path
        /// </summary>
        public void SaveDownloadFolderPath(string folderPath)
        {
            try
            {
                var settings = LoadSettings();
                settings.DownloadFolderPath = folderPath;
                SaveSettings(settings);
                Log.Information($"Saved download folder path to app directory: {folderPath}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving download folder path");
            }
        }

        /// <summary>
        /// Loads the saved download folder path, returns Desktop if none saved
        /// </summary>
        public string LoadDownloadFolderPath()
        {
            try
            {
                var settings = LoadSettings();
                if (!string.IsNullOrEmpty(settings.DownloadFolderPath) && Directory.Exists(settings.DownloadFolderPath))
                {
                    Log.Information($"Loaded saved download folder path from app directory: {settings.DownloadFolderPath}");
                    return settings.DownloadFolderPath;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading download folder path");
            }

            // Default to Desktop if no saved path or path doesn't exist
            var defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Log.Information($"Using default download folder path: {defaultPath}");
            return defaultPath;
        }

        /// <summary>
        /// Saves a file type association (extension -> program path)
        /// </summary>
        public void SaveFileTypeAssociation(string fileExtension, string programPath)
        {
            try
            {
                var settings = LoadSettings();
                settings.FileTypeAssociations[fileExtension.ToLowerInvariant()] = programPath;
                SaveSettings(settings);
                Log.Information($"Saved file type association: {fileExtension} -> {programPath}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving file type association for {Extension}", fileExtension);
            }
        }

        /// <summary>
        /// Loads the saved program path for a file extension, returns null if not found
        /// </summary>
        public string? LoadFileTypeAssociation(string fileExtension)
        {
            try
            {
                var settings = LoadSettings();
                var extension = fileExtension.ToLowerInvariant();
                if (settings.FileTypeAssociations.TryGetValue(extension, out var programPath))
                {
                    Log.Information($"Loaded file type association: {extension} -> {programPath}");
                    return programPath;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading file type association for {Extension}", fileExtension);
            }

            Log.Information($"No saved association found for file extension: {fileExtension}");
            return null;
        }

        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var jsonContent = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(jsonContent);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading settings from file");
            }

            return new AppSettings();
        }

        private void SaveSettings(AppSettings settings)
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(settings, _jsonOptions);
                File.WriteAllText(_settingsFilePath, jsonContent);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving settings to file");
                throw;
            }
        }
    }

    /// <summary>
    /// Application settings model
    /// </summary>
    public class AppSettings
    {
        public string DownloadFolderPath { get; set; } = string.Empty;
        public Dictionary<string, string> FileTypeAssociations { get; set; } = new Dictionary<string, string>();
    }
}
