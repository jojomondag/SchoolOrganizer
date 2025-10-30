using System;
using System.Diagnostics;
using System.IO;
using Serilog;
using SchoolOrganizer.Src.Models.Assignments;

namespace SchoolOrganizer.Src.Services;

/// <summary>
/// Service for handling file operations and editor selection
/// </summary>
public class FileHandlingService
{
    private readonly SettingsService _settingsService;

    public FileHandlingService()
    {
        _settingsService = SettingsService.Instance;
    }

    /// <summary>
    /// Opens a file with the appropriate application
    /// </summary>
    public bool OpenFile(StudentFile file)
    {
        if (file == null || string.IsNullOrEmpty(file.FilePath) || !File.Exists(file.FilePath))
        {
            Log.Warning("Cannot open file: {FileName}", file?.FileName ?? "null");
            return false;
        }

        try
        {
            var fileExtension = Path.GetExtension(file.FilePath).ToLowerInvariant();
            var savedProgramPath = _settingsService.LoadFileTypeAssociation(fileExtension);
            
            ProcessStartInfo startInfo;
            if (!string.IsNullOrEmpty(savedProgramPath) && savedProgramPath != "DEFAULT_SYSTEM")
            {
                startInfo = new ProcessStartInfo(savedProgramPath, $"\"{file.FilePath}\"");
            }
            else
            {
                startInfo = new ProcessStartInfo(file.FilePath) { UseShellExecute = true };
            }
            
            var process = Process.Start(startInfo);
            if (process != null)
            {
                // Save the association if we used the default system behavior
                if (string.IsNullOrEmpty(savedProgramPath))
                {
                    _settingsService.SaveFileTypeAssociation(fileExtension, "DEFAULT_SYSTEM");
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening file: {FilePath}", file.FilePath);
        }
        
        return false;
    }
}
