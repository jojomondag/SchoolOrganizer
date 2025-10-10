using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;
using SchoolOrganizer.Models;

namespace SchoolOrganizer.Services;

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

    /// <summary>
    /// Opens a file with a preferred code editor
    /// </summary>
    public bool OpenWithCodeEditor(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Log.Warning("File does not exist: {FilePath}", filePath);
            return false;
        }

        var editorInfo = FindPreferredEditor();
        if (editorInfo != null)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = editorInfo.Path,
                    Arguments = $"{editorInfo.Arguments} \"{filePath}\"",
                    UseShellExecute = false
                });
                return process != null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to open with preferred editor: {EditorPath}", editorInfo.Path);
            }
        }
        
        return false;
    }

    /// <summary>
    /// Gets a list of available code editors
    /// </summary>
    public List<EditorInfo> GetAvailableEditors()
    {
        var editors = new List<EditorInfo>();
        var commonEditors = new[]
        {
            new EditorInfo("Visual Studio Code", "code", ""),
            new EditorInfo("Visual Studio Code (Insiders)", "code-insiders", ""),
            new EditorInfo("Sublime Text", "subl", ""),
            new EditorInfo("Notepad++", "notepad++", ""),
            new EditorInfo("Vim", "vim", ""),
            new EditorInfo("Nano", "nano", ""),
            new EditorInfo("JetBrains Rider", "rider64", ""),
            new EditorInfo("Visual Studio", "devenv", "")
        };

        foreach (var editor in commonEditors)
        {
            if (IsEditorAvailable(editor.Path))
            {
                editors.Add(editor);
            }
        }

        return editors;
    }

    private EditorInfo? FindPreferredEditor()
    {
        var availableEditors = GetAvailableEditors();
        return availableEditors.FirstOrDefault();
    }

    private bool IsEditorAvailable(string editorPath)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = editorPath,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            
            if (process != null)
            {
                process.WaitForExit(2000); // Wait max 2 seconds
                return process.ExitCode == 0;
            }
        }
        catch
        {
            // Editor not available
        }
        
        return false;
    }
}

/// <summary>
/// Information about a code editor
/// </summary>
public class EditorInfo
{
    public string Name { get; }
    public string Path { get; }
    public string Arguments { get; }

    public EditorInfo(string name, string path, string arguments)
    {
        Name = name;
        Path = path;
        Arguments = arguments;
    }
}
