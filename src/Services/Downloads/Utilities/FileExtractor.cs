using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace SchoolOrganizer.Src.Services.Downloads.Utilities;

public static class FileExtractor
{
    public static async Task ExtractZipAndRARFilesFromFoldersAsync(string folderPath)
    {
        var archiveFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                    .Where(file => file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                                   file.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
                                    .ToList();

        if (archiveFiles.Count == 0)
            return;

        // Process archives in parallel for better performance
        var extractTasks = archiveFiles.Select(archiveFile => ExtractArchiveAsync(archiveFile));
        await Task.WhenAll(extractTasks);
    }

    private static async Task ExtractArchiveAsync(string archiveFile)
    {
        await Task.Run(() =>
        {
            try
            {
                string extractPath = Path.Combine(Path.GetDirectoryName(archiveFile)!, Path.GetFileNameWithoutExtension(archiveFile));
                Directory.CreateDirectory(extractPath);

                if (archiveFile.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ZipFile.ExtractToDirectory(archiveFile, extractPath, overwriteFiles: true);
                }
                else if (archiveFile.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
                {
                    using var archive = ArchiveFactory.Open(archiveFile);
                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.IsDirectory)
                        {
                            entry.WriteToDirectory(extractPath, new ExtractionOptions()
                            {
                                ExtractFullPath = true,
                                Overwrite = true
                            });
                        }
                    }
                }
                Log.Information($"Extracted {archiveFile} to {extractPath}.");

                // Create a link to the extracted folder
                string linkFileName = Path.GetFileNameWithoutExtension(archiveFile) + "_extracted.url";
                string linkFilePath = Path.Combine(Path.GetDirectoryName(archiveFile)!, linkFileName);
                CreateShortcutToFolder(linkFilePath, extractPath);
                Log.Information($"Created link to extracted folder: {linkFilePath}");

                // Remove the archive file after successful extraction
                File.Delete(archiveFile);
                Log.Information($"Deleted archive file: {archiveFile}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error extracting {archiveFile}: {ex.Message}");
            }
        });
    }

    private static void CreateShortcutToFolder(string shortcutPath, string targetPath)
    {
        string shortcutContent = $@"[InternetShortcut]
URL=file://{targetPath.Replace('\\', '/')}
IconFile=C:\Windows\System32\shell32.dll
IconIndex=3
";
        File.WriteAllText(shortcutPath, shortcutContent);
    }
}

