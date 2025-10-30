using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Serilog;

namespace SchoolOrganizer.Src.Services
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

        /// <summary>
        /// Saves the auto-sync enabled setting
        /// </summary>
        public void SaveAutoSyncEnabled(bool enabled)
        {
            try
            {
                var settings = LoadSettings();
                settings.AutoSyncEnabled = enabled;
                SaveSettings(settings);
                Log.Information($"Saved auto-sync enabled: {enabled}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving auto-sync enabled setting");
            }
        }

        /// <summary>
        /// Loads the auto-sync enabled setting
        /// </summary>
        public bool? GetAutoSyncEnabled()
        {
            try
            {
                var settings = LoadSettings();
                return settings.AutoSyncEnabled;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading auto-sync enabled setting");
                return null;
            }
        }

        /// <summary>
        /// Saves the sync interval in minutes
        /// </summary>
        public void SaveSyncIntervalMinutes(int minutes)
        {
            try
            {
                var settings = LoadSettings();
                settings.SyncIntervalMinutes = minutes;
                SaveSettings(settings);
                Log.Information($"Saved sync interval: {minutes} minutes");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving sync interval");
            }
        }

        /// <summary>
        /// Loads the sync interval in minutes
        /// </summary>
        public int? GetSyncIntervalMinutes()
        {
            try
            {
                var settings = LoadSettings();
                return settings.SyncIntervalMinutes;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading sync interval");
                return null;
            }
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

        /// <summary>
        /// Saves window bounds (position and size)
        /// </summary>
        public void SaveWindowBounds(string windowName, double x, double y, double width, double height, int windowState)
        {
            try
            {
                Log.Information("[SettingsService] Saving window bounds for {WindowName}: {X},{Y} {Width}x{Height}, State: {State}", 
                    windowName, x, y, width, height, windowState);
                
                var settings = LoadSettings();
                
                Log.Information("[SettingsService] Current bounds in settings before update: {Bounds}", 
                    settings.WindowBounds.ContainsKey(windowName) 
                        ? $"X={settings.WindowBounds[windowName].X}, Y={settings.WindowBounds[windowName].Y}, W={settings.WindowBounds[windowName].Width}, H={settings.WindowBounds[windowName].Height}, State={settings.WindowBounds[windowName].WindowState}"
                        : "Not set");
                
                settings.WindowBounds[windowName] = new WindowBounds 
                { 
                    X = x, 
                    Y = y, 
                    Width = width, 
                    Height = height,
                    WindowState = windowState
                };
                
                Log.Information("[SettingsService] Updated bounds in memory to: X={X}, Y={Y}, W={W}, H={H}, State={State}", 
                    x, y, width, height, windowState);
                
                SaveSettings(settings);
                
                Log.Information("[SettingsService] SaveSettings completed - bounds written to file");
                
                // Verify it was saved correctly
                var verifySettings = LoadSettings();
                if (verifySettings.WindowBounds.ContainsKey(windowName))
                {
                    var saved = verifySettings.WindowBounds[windowName];
                    Log.Information("[SettingsService] VERIFIED saved bounds: X={X}, Y={Y}, W={W}, H={H}, State={State}", 
                        saved.X, saved.Y, saved.Width, saved.Height, saved.WindowState);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving window bounds for {WindowName}", windowName);
            }
        }

        /// <summary>
        /// Loads window bounds (position and size)
        /// </summary>
        public WindowBounds? LoadWindowBounds(string windowName)
        {
            try
            {
                var settings = LoadSettings();
                if (settings.WindowBounds.TryGetValue(windowName, out var bounds))
                {
                    Log.Information("Loaded window bounds for {WindowName}: {X},{Y} {Width}x{Height}", 
                        windowName, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    return bounds;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading window bounds for {WindowName}", windowName);
            }

            Log.Information("No saved bounds found for window: {WindowName}", windowName);
            return null;
        }

        /// <summary>
        /// Saves the assignment view mode preference (embedded vs detached)
        /// </summary>
        public void SaveAssignmentViewPreference(bool preferEmbedded)
        {
            try
            {
                var settings = LoadSettings();
                settings.PreferEmbeddedAssignmentView = preferEmbedded;
                SaveSettings(settings);
                Log.Information("Saved assignment view preference: {Preference}", preferEmbedded ? "Embedded" : "Detached");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving assignment view preference");
            }
        }

        /// <summary>
        /// Loads the assignment view mode preference
        /// </summary>
        public bool LoadAssignmentViewPreference()
        {
            try
            {
                var settings = LoadSettings();
                Log.Information("Loaded assignment view preference: {Preference}", 
                    settings.PreferEmbeddedAssignmentView ? "Embedded" : "Detached");
                return settings.PreferEmbeddedAssignmentView;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading assignment view preference");
                return true; // Default to embedded
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
        public bool AutoSyncEnabled { get; set; } = false;
        public int SyncIntervalMinutes { get; set; } = 30;
        public Dictionary<string, WindowBounds> WindowBounds { get; set; } = new Dictionary<string, WindowBounds>();
        public bool PreferEmbeddedAssignmentView { get; set; } = true; // Default to embedded
    }

    /// <summary>
    /// Window bounds model for storing position and size
    /// </summary>
    public class WindowBounds
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int WindowState { get; set; } // 0=Normal, 1=Minimized, 2=Maximized, 3=FullScreen
    }
}
