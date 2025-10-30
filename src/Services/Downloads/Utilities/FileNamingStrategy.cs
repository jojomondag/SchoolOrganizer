using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SchoolOrganizer.Src.Services.Downloads.Utilities;

namespace SchoolOrganizer.Src.Services.Downloads.Utilities;

/// <summary>
/// Handles intelligent file naming to avoid duplicates while keeping folder names human-readable.
/// Only creates subfolders when filenames are EXACTLY the same (case-sensitive).
/// </summary>
public static class FileNamingStrategy
{
    /// <summary>
    /// Determines the folder path for a file, creating subfolders only when filenames are exactly the same
    /// </summary>
    /// <param name="fileId">Google Drive file ID</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="destinationFolder">Base destination folder</param>
    /// <param name="existingFileIds">Set of file IDs already downloaded in this folder</param>
    /// <param name="existingFileNames">Set of exact filenames (case-sensitive) already used in this folder</param>
    /// <returns>Full path to the file (may include subfolder if needed for exact duplicates)</returns>
    public static string GetFilePathForDownload(
        string fileId,
        string fileName,
        string destinationFolder,
        HashSet<string> existingFileIds,
        HashSet<string>? existingFileNames = null)
    {
        // Sanitize the filename
        string sanitizedFileName = DirectoryUtil.SanitizeFolderName(Path.GetFileNameWithoutExtension(fileName));
        string fileExtension = Path.GetExtension(fileName);
        string finalFileName = $"{sanitizedFileName}{fileExtension}";

        // Check if this exact file was already downloaded (same file ID)
        // If so, we can overwrite it
        if (existingFileIds.Contains(fileId))
        {
            // Same file - can overwrite directly
            existingFileNames?.Add(finalFileName);
            return Path.Combine(destinationFolder, finalFileName);
        }

        // Check if a file with the EXACT same name (case-sensitive) already exists
        // Only create subfolder if the filename is exactly the same
        string directPath = Path.Combine(destinationFolder, finalFileName);
        
        // Check both file system and tracked filenames for exact matches
        bool exactDuplicateExists = false;
        
        if (existingFileNames != null)
        {
            // Check if we've already tracked this exact filename (case-sensitive)
            exactDuplicateExists = existingFileNames.Contains(finalFileName);
        }
        
        // Also check the file system for exact matches (case-sensitive comparison)
        if (!exactDuplicateExists && Directory.Exists(destinationFolder))
        {
            var filesInFolder = Directory.GetFiles(destinationFolder, "*", SearchOption.TopDirectoryOnly);
            exactDuplicateExists = filesInFolder.Any(f => 
                string.Equals(Path.GetFileName(f), finalFileName, StringComparison.Ordinal));
        }
        
        if (!exactDuplicateExists)
        {
            // No exact duplicate - can save directly
            existingFileNames?.Add(finalFileName);
            return directPath;
        }

        // Exact duplicate exists - need to create a subfolder
        // Use filename-based folder name with counter for duplicates
        string folderName = GetUniqueFolderName(destinationFolder, sanitizedFileName);
        string fileFolder = Path.Combine(destinationFolder, folderName);
        Directory.CreateDirectory(fileFolder);

        return Path.Combine(fileFolder, finalFileName);
    }

    /// <summary>
    /// Gets a unique folder name based on the filename, with a counter if needed
    /// Example: "MyFile", "MyFile_2", "MyFile_3", etc.
    /// </summary>
    public static string GetUniqueFolderName(string destinationFolder, string baseFileName)
    {
        // First try just the filename
        string folderName = baseFileName;
        string folderPath = Path.Combine(destinationFolder, folderName);

        if (!Directory.Exists(folderPath))
        {
            return folderName;
        }

        // If folder exists, try with counter
        int counter = 2;
        while (true)
        {
            folderName = $"{baseFileName}_{counter}";
            folderPath = Path.Combine(destinationFolder, folderName);
            
            if (!Directory.Exists(folderPath))
            {
                return folderName;
            }

            counter++;
            
            // Safety limit to prevent infinite loop
            if (counter > 1000)
            {
                // Fallback to timestamp-based name
                return $"{baseFileName}_{DateTime.Now:yyyyMMddHHmmss}";
            }
        }
    }

    /// <summary>
    /// Gets just the folder path (for cases where we need to create a folder first)
    /// </summary>
    public static string GetFolderPathForFile(
        string fileId,
        string fileName,
        string destinationFolder,
        HashSet<string> existingFileIds,
        HashSet<string>? existingFileNames = null)
    {
        string filePath = GetFilePathForDownload(fileId, fileName, destinationFolder, existingFileIds, existingFileNames);
        return Path.GetDirectoryName(filePath) ?? destinationFolder;
    }
}

