using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DiffPlex;
using DiffPlex.DiffBuilder;
using FuzzySharp;
using Serilog;
using SchoolOrganizer.Src.Models.Plagiarism;
using SchoolOrganizer.Src.Models.Assignments;
using SchoolOrganizer.Src.Services.Utilities;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using ImageInfo = SchoolOrganizer.Src.Models.Plagiarism.ImageInfo;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace SchoolOrganizer.Src.Services;

public class PlagiarismDetectionService : IPlagiarismDetectionService
{
    private readonly IDiffer _differ;
    private readonly InlineDiffBuilder _diffBuilder;

    public PlagiarismDetectionService()
    {
        _differ = new Differ();
        _diffBuilder = new InlineDiffBuilder(_differ);
    }

    public async Task<List<PlagiarismResult>> AnalyzeCourseForPlagiarismAsync(string courseDirectory, PlagiarismAnalysisSettings? settings = null)
    {
        settings ??= new PlagiarismAnalysisSettings();
        var results = new List<PlagiarismResult>();

        if (!Directory.Exists(courseDirectory))
        {
            Log.Warning($"Course directory does not exist: {courseDirectory}");
            return results;
        }

        Log.Information($"Starting plagiarism analysis for course: {courseDirectory}");

        try
        {
            var studentDirectories = Directory.GetDirectories(courseDirectory);
            Log.Information($"Found {studentDirectories.Length} student directories");

            var allFileData = new List<FileAnalysisData>();

            foreach (var studentDir in studentDirectories)
            {
                var studentName = Path.GetFileName(studentDir);
                Log.Information($"Processing student directory: {studentName}");

                var studentFiles = await GetStudentFilesAsync(studentDir, settings);
                Log.Information($"Found {studentFiles.Count} files for student: {studentName}");

                foreach (var filePath in studentFiles)
                {
                    Log.Information($"Extracting data from file: {Path.GetFileName(filePath)}");

                    var fileData = await ExtractFileDataAsync(filePath);
                    if (fileData != null)
                    {
                        fileData.StudentName = studentName;
                        fileData.AssignmentName = ExtractAssignmentNameFromPath(filePath, studentDir);
                        allFileData.Add(fileData);

                        Log.Information($"Successfully extracted data for: {fileData.FileName}");
                    }
                }
            }

            Log.Information($"Found {allFileData.Count} files to analyze across {studentDirectories.Length} students");

            if (allFileData.Count == 0)
            {
                Log.Warning("No files found to analyze");
                return results;
            }

            results = await CompareAllFilesAsync(allFileData, settings);

            Log.Information($"Plagiarism analysis completed. Found {results.Count} results with potential issues");

            if (results.Any())
            {
                Log.Information("Running deep verification analysis on flagged files...");
                results = RunDeepVerification(results, allFileData, settings);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error during plagiarism analysis for course: {courseDirectory}");
        }

        return results;
    }

    public async Task<List<PlagiarismResult>> AnalyzeAssignmentForPlagiarismAsync(string assignmentDirectory, PlagiarismAnalysisSettings? settings = null)
    {
        settings ??= new PlagiarismAnalysisSettings();
        var results = new List<PlagiarismResult>();

        if (!Directory.Exists(assignmentDirectory))
        {
            Log.Warning($"Assignment directory does not exist: {assignmentDirectory}");
            return results;
        }

        Log.Information($"Starting plagiarism analysis for assignment: {assignmentDirectory}");

        try
        {
            var allFiles = await GetFilesInDirectoryAsync(assignmentDirectory, settings);
            var allFileData = new List<FileAnalysisData>();

            foreach (var filePath in allFiles)
            {
                var fileData = await ExtractFileDataAsync(filePath);
                if (fileData != null)
                {
                    var (studentName, assignmentName) = ExtractStudentAndAssignmentFromPath(filePath, assignmentDirectory);
                    fileData.StudentName = studentName;
                    fileData.AssignmentName = assignmentName;
                    allFileData.Add(fileData);
                }
            }

            results = await CompareAllFilesAsync(allFileData, settings);

            Log.Information($"Assignment plagiarism analysis completed. Found {results.Count} potential issues");

            if (results.Any())
            {
                results = RunDeepVerification(results, allFileData, settings);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error during assignment plagiarism analysis: {assignmentDirectory}");
        }

        return results;
    }

    public async Task<PlagiarismMatch> CompareFilesAsync(string file1Path, string file2Path, PlagiarismAnalysisSettings? settings = null)
    {
        settings ??= new PlagiarismAnalysisSettings();

        var file1Data = await ExtractFileDataAsync(file1Path);
        var file2Data = await ExtractFileDataAsync(file2Path);

        if (file1Data == null || file2Data == null)
        {
            return new PlagiarismMatch
            {
                ComparedToFileName = Path.GetFileName(file2Path),
                ComparedToFilePath = file2Path,
                SimilarityScore = 0,
                MatchType = "Error"
            };
        }

        return CompareFileDataAsync(file1Data, file2Data, settings);
    }

    public async Task<FileAnalysisData?> ExtractFileDataAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Log.Warning($"File does not exist: {filePath}");
                return null;
            }

            var fileInfo = new FileInfo(filePath);

            const long maxFileSize = 5 * 1024 * 1024;
            if (fileInfo.Length > maxFileSize)
            {
                return new FileAnalysisData
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    CreatedDate = fileInfo.CreationTime,
                    ModifiedDate = fileInfo.LastWriteTime,
                    FileExtension = fileInfo.Extension.ToLowerInvariant(),
                    Hash = await CalculateFileHashAsync(filePath),
                    Content = string.Empty,
                    WordCount = 0,
                    CharacterCount = 0
                };
            }

            var data = new FileAnalysisData
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                FileSize = fileInfo.Length,
                CreatedDate = fileInfo.CreationTime,
                ModifiedDate = fileInfo.LastWriteTime,
                FileExtension = fileInfo.Extension.ToLowerInvariant(),
                Hash = await CalculateFileHashAsync(filePath)
            };

            data.Content = await ExtractTextContentAsync(filePath, data.FileExtension);

            if (!string.IsNullOrEmpty(data.Content))
            {
                if (data.Content.Length > 100000)
                {
                    data.Content = data.Content.Substring(0, 100000);
                }

                data.TextLines = data.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                data.WordCount = CountWords(data.Content);
                data.CharacterCount = data.Content.Length;
                data.UniqueWords = ExtractUniqueWords(data.Content);
            }

            if (data.FileExtension == ".docx")
            {
                try
                {
                    data.Images = ExtractImagesFromDocxAsync(filePath).Result;
                    data.StructuralSignature = await GenerateStructuralSignatureAsync(filePath);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, $"Could not extract images from DOCX: {filePath}");
                }
            }

            return data;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error extracting file data from: {filePath}");
            return null;
        }
    }

    public double CalculateTextSimilarity(string content1, string content2)
    {
        if (string.IsNullOrEmpty(content1) || string.IsNullOrEmpty(content2))
            return 0;

        const int maxLength = 50000;
        if (content1.Length > maxLength) content1 = content1.Substring(0, maxLength);
        if (content2.Length > maxLength) content2 = content2.Substring(0, maxLength);

        var normalized1 = NormalizeText(content1);
        var normalized2 = NormalizeText(content2);

        if (normalized1.Length < 100 || normalized2.Length < 100)
        {
            return string.Equals(normalized1, normalized2, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
        }

        try
        {
            var words1 = ExtractUniqueWords(normalized1);
            var words2 = ExtractUniqueWords(normalized2);
            var jaccardSimilarity = CalculateJaccardSimilarity(words1, words2);

            if (jaccardSimilarity < 0.3)
            {
                return jaccardSimilarity;
            }

            var tokenSetRatio = Fuzz.TokenSetRatio(normalized1, normalized2) / 100.0;
            var tokenSortRatio = Fuzz.TokenSortRatio(normalized1, normalized2) / 100.0;

            var combinedScore = (tokenSetRatio * 0.4) + (tokenSortRatio * 0.3) + (jaccardSimilarity * 0.3);

            return Math.Min(1.0, combinedScore);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error during text similarity calculation");
            return string.Equals(normalized1, normalized2, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
        }
    }

    public async Task<string> CalculateFileHashAsync(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var md5 = MD5.Create();
            var hash = await md5.ComputeHashAsync(stream);
            return Convert.ToHexString(hash);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error calculating hash for file: {filePath}");
            return string.Empty;
        }
    }

    private Task<List<string>> GetStudentFilesAsync(string studentDirectory, PlagiarismAnalysisSettings settings)
    {
        var files = new List<string>();

        try
        {
            var allFiles = Directory.GetFiles(studentDirectory, "*", SearchOption.AllDirectories);

            foreach (var file in allFiles)
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (settings.SupportedFileExtensions.Contains(extension))
                {
                    files.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error getting files from student directory: {studentDirectory}");
        }

        return Task.FromResult(files);
    }

    private Task<List<string>> GetFilesInDirectoryAsync(string directory, PlagiarismAnalysisSettings settings)
    {
        var files = new List<string>();

        try
        {
            var allFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);

            foreach (var file in allFiles)
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (settings.SupportedFileExtensions.Contains(extension))
                {
                    files.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error getting files from directory: {directory}");
        }

        return Task.FromResult(files);
    }

    private Task<List<PlagiarismResult>> CompareAllFilesAsync(List<FileAnalysisData> allFileData, PlagiarismAnalysisSettings settings)
    {
        var results = new List<PlagiarismResult>();
        var skippedSelfComparisons = 0;

        var filesByAssignment = allFileData
            .GroupBy(f => f.AssignmentName)
            .Where(g => g.Count() > 1);

        foreach (var assignmentGroup in filesByAssignment)
        {
            var assignmentFiles = assignmentGroup.ToList();

            for (int i = 0; i < assignmentFiles.Count; i++)
            {
                var currentFile = assignmentFiles[i];

                var plagiarismResult = new PlagiarismResult
                {
                    StudentName = currentFile.StudentName,
                    FileName = currentFile.FileName,
                    FilePath = currentFile.FilePath,
                    AssignmentName = currentFile.AssignmentName
                };

                for (int j = 0; j < assignmentFiles.Count; j++)
                {
                    if (i == j) continue;

                    var comparedFile = assignmentFiles[j];

                    if (currentFile.StudentName == comparedFile.StudentName)
                    {
                        skippedSelfComparisons++;
                        continue;
                    }

                    var match = CompareFileDataAsync(currentFile, comparedFile, settings);

                    if (match.SimilarityScore >= settings.ContentSimilarityThreshold)
                    {
                        plagiarismResult.Matches.Add(match);
                    }
                }

                if (plagiarismResult.Matches.Any())
                {
                    plagiarismResult.OverallSimilarityScore = plagiarismResult.Matches.Max(m => m.SimilarityScore);
                    results.Add(plagiarismResult);
                }
            }
        }

        return Task.FromResult(results);
    }

    private PlagiarismMatch CompareFileDataAsync(FileAnalysisData file1, FileAnalysisData file2, PlagiarismAnalysisSettings settings)
    {
        if (file1.StudentName == file2.StudentName)
        {
            return new PlagiarismMatch
            {
                ComparedToStudentName = file2.StudentName,
                ComparedToFileName = file2.FileName,
                ComparedToFilePath = file2.FilePath,
                MatchId = $"BLOCKED_SELF_MATCH_{file1.StudentName}",
                SimilarityScore = 0.0
            };
        }

        var matchId = $"{file1.StudentName}_{file1.FileName}--{file2.StudentName}_{file2.FileName}";

        var match = new PlagiarismMatch
        {
            ComparedToStudentName = file2.StudentName,
            ComparedToFileName = file2.FileName,
            ComparedToFilePath = file2.FilePath,
            MatchId = matchId
        };

        var indicators = new List<SimilarityIndicator>();

        if (settings.AnalyzeFileSize)
        {
            var sizeIndicator = CompareFileSize(file1, file2, settings);
            if (sizeIndicator.Score > 0)
                indicators.Add(sizeIndicator);
        }

        if (settings.AnalyzeFileHashes && !string.IsNullOrEmpty(file1.Hash) && !string.IsNullOrEmpty(file2.Hash))
        {
            var hashIndicator = CompareFileHashes(file1, file2);
            if (hashIndicator.Score > 0)
                indicators.Add(hashIndicator);
        }

        if (settings.AnalyzeTextContent && !string.IsNullOrEmpty(file1.Content) && !string.IsNullOrEmpty(file2.Content))
        {
            var contentIndicator = CompareTextContent(file1, file2, settings);
            if (contentIndicator.Score > 0)
                indicators.Add(contentIndicator);
        }

        if (settings.AnalyzeFileMetadata)
        {
            var metadataIndicator = CompareMetadata(file1, file2);
            if (metadataIndicator.Score > 0)
                indicators.Add(metadataIndicator);
        }

        if (settings.AnalyzeImages && file1.Images.Any() && file2.Images.Any())
        {
            var imageIndicator = CompareImages(file1, file2);
            if (imageIndicator.Score > 0)
                indicators.Add(imageIndicator);
        }

        if (settings.AnalyzeStructuralSimilarity && !string.IsNullOrEmpty(file1.StructuralSignature) && !string.IsNullOrEmpty(file2.StructuralSignature))
        {
            var structuralIndicator = CompareStructuralSignatures(file1, file2);
            if (structuralIndicator.Score > 0)
                indicators.Add(structuralIndicator);
        }

        match.Indicators = indicators;

        if (indicators.Any())
        {
            match.SimilarityScore = indicators.Average(i => i.Score);
            match.MatchType = string.Join(", ", indicators.Select(i => i.Type).Distinct());
        }

        return match;
    }

    private SimilarityIndicator CompareFileSize(FileAnalysisData file1, FileAnalysisData file2, PlagiarismAnalysisSettings settings)
    {
        var sizeDifference = Math.Abs(file1.FileSize - file2.FileSize);
        var largerFileSize = Math.Max(file1.FileSize, file2.FileSize);
        var relativeExactSizeDifference = largerFileSize > 0 ? (double)sizeDifference / largerFileSize : 0;

        var score = relativeExactSizeDifference <= settings.FileSizeTolerancePercent ? 1.0 : 0.0;

        return new SimilarityIndicator
        {
            Type = "FileSize",
            Description = $"File size: {file1.FileSize} vs {file2.FileSize} bytes",
            Score = score,
            Evidence = $"Difference: {sizeDifference} bytes ({relativeExactSizeDifference:P1})"
        };
    }

    private SimilarityIndicator CompareFileHashes(FileAnalysisData file1, FileAnalysisData file2)
    {
        var score = string.Equals(file1.Hash, file2.Hash, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;

        return new SimilarityIndicator
        {
            Type = "Hash",
            Description = "MD5 hash comparison",
            Score = score,
            Evidence = score > 0 ? "Identical MD5 hash values" : "Different MD5 hash values"
        };
    }

    private SimilarityIndicator CompareTextContent(FileAnalysisData file1, FileAnalysisData file2, PlagiarismAnalysisSettings settings)
    {
        var similarity = 0.0;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.ComparisonTimeoutSeconds));

            var task = Task.Run(() => CalculateTextSimilarity(file1.Content, file2.Content), cts.Token);

            similarity = task.Wait(TimeSpan.FromSeconds(settings.ComparisonTimeoutSeconds))
                ? task.Result
                : 0.0;

            if (cts.Token.IsCancellationRequested)
            {
                Log.Warning($"Text comparison timed out between {file1.FileName} and {file2.FileName}");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, $"Error comparing text content between {file1.FileName} and {file2.FileName}");
            similarity = 0.0;
        }

        return new SimilarityIndicator
        {
            Type = "Content",
            Description = "Text content similarity",
            Score = similarity,
            Evidence = $"Text similarity: {similarity:P1}"
        };
    }

    private SimilarityIndicator CompareMetadata(FileAnalysisData file1, FileAnalysisData file2)
    {
        var score = 0.0;
        var evidence = new List<string>();

        if (file1.WordCount > 0 && file2.WordCount > 0)
        {
            var wordCountDiff = Math.Abs(file1.WordCount - file2.WordCount);
            var maxWordCount = Math.Max(file1.WordCount, file2.WordCount);
            var wordCountSimilarity = 1.0 - ((double)wordCountDiff / maxWordCount);

            if (wordCountSimilarity > 0.9)
            {
                score += 0.5;
                evidence.Add($"Similar word count: {file1.WordCount} vs {file2.WordCount}");
            }
        }

        if (file1.CharacterCount > 0 && file2.CharacterCount > 0)
        {
            var charCountDiff = Math.Abs(file1.CharacterCount - file2.CharacterCount);
            var maxCharCount = Math.Max(file1.CharacterCount, file2.CharacterCount);
            var charCountSimilarity = 1.0 - ((double)charCountDiff / maxCharCount);

            if (charCountSimilarity > 0.9)
            {
                score += 0.5;
                evidence.Add($"Similar character count: {file1.CharacterCount} vs {file2.CharacterCount}");
            }
        }

        return new SimilarityIndicator
        {
            Type = "Metadata",
            Description = "File metadata comparison",
            Score = Math.Min(1.0, score),
            Evidence = string.Join("; ", evidence)
        };
    }

    private SimilarityIndicator CompareImages(FileAnalysisData file1, FileAnalysisData file2)
    {
        var score = 0.0;
        var evidence = new List<string>();

        var exactMatches = 0;
        var perceptualMatches = 0;
        var metadataMatches = 0;

        foreach (var img1 in file1.Images)
        {
            foreach (var img2 in file2.Images)
            {
                if (!string.IsNullOrEmpty(img1.ImageHash) && img1.ImageHash == img2.ImageHash)
                {
                    exactMatches++;
                    evidence.Add($"Identical image found: {img1.Width}x{img1.Height}");
                    continue;
                }

                if (!string.IsNullOrEmpty(img1.PerceptualHash) && !string.IsNullOrEmpty(img2.PerceptualHash))
                {
                    var similarity = CalculatePerceptualSimilarity(img1.PerceptualHash, img2.PerceptualHash);
                    if (similarity > 0.85)
                    {
                        perceptualMatches++;
                        evidence.Add($"Similar image found: {similarity:P0} similarity");
                    }
                }

                var metadataScore = CompareImageMetadata(img1, img2);
                if (metadataScore > 0.7)
                {
                    metadataMatches++;
                    evidence.Add($"Image metadata match: {metadataScore:P0}");
                }
            }
        }

        var totalImages = Math.Max(file1.Images.Count, file2.Images.Count);
        if (totalImages > 0)
        {
            score = (exactMatches * 1.0 + metadataMatches * 0.9 + perceptualMatches * 0.7) / totalImages;
            score = Math.Min(1.0, score);
        }

        return new SimilarityIndicator
        {
            Type = "Images",
            Description = "Image analysis with metadata",
            Score = score,
            Evidence = evidence.Any() ? string.Join("; ", evidence) : "No image matches"
        };
    }

    private SimilarityIndicator CompareStructuralSignatures(FileAnalysisData file1, FileAnalysisData file2)
    {
        var similarity = CalculateStringSimilarity(file1.StructuralSignature, file2.StructuralSignature);

        return new SimilarityIndicator
        {
            Type = "Structure",
            Description = "Document structure",
            Score = similarity,
            Evidence = $"Structural similarity: {similarity:P0}"
        };
    }

    private double CalculatePerceptualSimilarity(string hash1, string hash2)
    {
        if (hash1.Length != hash2.Length) return 0;

        var matches = 0;
        for (int i = 0; i < hash1.Length; i++)
        {
            if (hash1[i] == hash2[i]) matches++;
        }

        return (double)matches / hash1.Length;
    }

    private double CalculateStringSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2)) return 0;

        var maxLength = Math.Max(str1.Length, str2.Length);
        if (maxLength == 0) return 1.0;

        var distance = ComputeLevenshteinDistance(str1, str2);
        return 1.0 - ((double)distance / maxLength);
    }

    private int ComputeLevenshteinDistance(string s1, string s2)
    {
        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }

    private async Task<string> ExtractTextContentAsync(string filePath, string fileExtension)
    {
        try
        {
            return fileExtension switch
            {
                ".txt" or ".md" or ".html" or ".css" or ".js" or ".json" or ".xml" => await File.ReadAllTextAsync(filePath),
                ".docx" => await ExtractDocxContentAsync(filePath),
                ".pdf" => await ExtractPdfContentAsync(filePath),
                _ => string.Empty
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error extracting text content from: {filePath}");
            return string.Empty;
        }
    }

    private Task<string> ExtractDocxContentAsync(string filePath)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return Task.FromResult(string.Empty);

            var textBuilder = new StringBuilder();
            foreach (var paragraph in body.Elements<Paragraph>())
            {
                foreach (var text in paragraph.Descendants<Text>())
                {
                    textBuilder.AppendLine(text.Text);
                }
            }

            return Task.FromResult(textBuilder.ToString());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, $"Error extracting DOCX content from: {filePath}");
            return Task.FromResult(string.Empty);
        }
    }

    private Task<string> ExtractPdfContentAsync(string filePath)
    {
        Log.Information($"PDF content extraction not yet implemented for: {filePath}");
        return Task.FromResult(string.Empty);
    }

    private string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var normalized = Regex.Replace(text.ToLowerInvariant(), @"\s+", " ").Trim();
        normalized = Regex.Replace(normalized, @"[^\w\s]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized;
    }

    private int CountWords(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private List<string> ExtractUniqueWords(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string>();

        var normalized = NormalizeText(text);
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return words.Where(w => w.Length > 2).Distinct().ToList();
    }

    private double CalculateJaccardSimilarity(List<string> set1, List<string> set2)
    {
        if (!set1.Any() && !set2.Any())
            return 1.0;

        if (!set1.Any() || !set2.Any())
            return 0.0;

        var intersection = set1.Intersect(set2).Count();
        var union = set1.Union(set2).Count();

        return (double)intersection / union;
    }

    private string ExtractAssignmentNameFromPath(string filePath, string studentDir)
    {
        try
        {
            var relativePath = Path.GetRelativePath(studentDir, filePath);
            var pathParts = relativePath.Split(Path.DirectorySeparatorChar);

            return pathParts.Length > 1 ? pathParts[0] : "Unknown Assignment";
        }
        catch
        {
            return "Unknown Assignment";
        }
    }

    private (string studentName, string assignmentName) ExtractStudentAndAssignmentFromPath(string filePath, string basePath)
    {
        try
        {
            var relativePath = Path.GetRelativePath(basePath, filePath);
            var pathParts = relativePath.Split(Path.DirectorySeparatorChar);

            if (pathParts.Length >= 2)
            {
                return (pathParts[0], pathParts[1]);
            }
            else if (pathParts.Length == 1)
            {
                return (pathParts[0], "Unknown Assignment");
            }

            return ("Unknown Student", "Unknown Assignment");
        }
        catch
        {
            return ("Unknown Student", "Unknown Assignment");
        }
    }

    private Task<List<ImageInfo>> ExtractImagesFromDocxAsync(string filePath)
    {
        var images = new List<ImageInfo>();

        try
        {
            using var doc = WordprocessingDocument.Open(filePath, false);
            var mainPart = doc.MainDocumentPart;
            if (mainPart == null) return Task.FromResult(images);

            var imageParts = mainPart.ImageParts;

            foreach (var imagePart in imageParts)
            {
                try
                {
                    using var stream = imagePart.GetStream();
                    using var memoryStream = new MemoryStream();
                    stream.CopyTo(memoryStream);
                    var imageBytes = memoryStream.ToArray();

                    var imageHash = CalculateImageHash(imageBytes);

                    try
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        using var image = Image.Load<Rgba32>(memoryStream);

                        memoryStream.Seek(0, SeekOrigin.Begin);
                        var metadata = ExtractImageMetadata(memoryStream);

                        var imageInfo = new ImageInfo
                        {
                            ImageHash = imageHash,
                            Width = image.Width,
                            Height = image.Height,
                            Size = imageBytes.Length,
                            Format = GetImageFormat(imagePart.ContentType),
                            PerceptualHash = CalculatePerceptualHash(image),
                            Metadata = metadata.generalMetadata,
                            DateTaken = metadata.dateTaken,
                            CameraModel = metadata.cameraModel,
                            CameraMake = metadata.cameraMake,
                            Software = metadata.software,
                            Latitude = metadata.latitude,
                            Longitude = metadata.longitude,
                            Artist = metadata.artist,
                            Copyright = metadata.copyright
                        };

                        images.Add(imageInfo);
                    }
                    catch (Exception)
                    {
                        images.Add(new ImageInfo
                        {
                            ImageHash = imageHash,
                            Size = imageBytes.Length,
                            Format = GetImageFormat(imagePart.ContentType)
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error processing image from DOCX");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error extracting images from DOCX: {filePath}");
        }

        return Task.FromResult(images);
    }

    private Task<string> GenerateStructuralSignatureAsync(string filePath)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return Task.FromResult(string.Empty);

            var signature = new StringBuilder();

            foreach (var element in body.Elements())
            {
                if (element is Paragraph paragraph)
                {
                    signature.Append("P");
                    if (paragraph.ParagraphProperties?.Justification != null)
                        signature.Append($"[{paragraph.ParagraphProperties.Justification.Val}]");
                }
                else if (element is Table)
                {
                    signature.Append("T");
                }
            }

            return Task.FromResult(signature.ToString());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, $"Error generating structural signature for: {filePath}");
            return Task.FromResult(string.Empty);
        }
    }

    private string CalculateImageHash(byte[] imageBytes)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(imageBytes);
        return Convert.ToHexString(hash);
    }

    private string CalculatePerceptualHash(Image<Rgba32> image)
    {
        try
        {
            using var resized = image.Clone();
            resized.Mutate(x => x.Resize(8, 8));
            var hash = new StringBuilder();

            var avgBrightness = 0.0;
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    var pixel = resized[x, y];
                    avgBrightness += (pixel.R + pixel.G + pixel.B) / 3.0;
                }
            }
            avgBrightness /= 64;

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    var pixel = resized[x, y];
                    var brightness = (pixel.R + pixel.G + pixel.B) / 3.0;
                    hash.Append(brightness >= avgBrightness ? "1" : "0");
                }
            }

            return hash.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private string GetImageFormat(string contentType)
    {
        return contentType switch
        {
            "image/jpeg" => "JPEG",
            "image/png" => "PNG",
            "image/gif" => "GIF",
            "image/bmp" => "BMP",
            _ => "Unknown"
        };
    }

    private (Dictionary<string, string> generalMetadata, DateTime? dateTaken, string? cameraModel, string? cameraMake,
             string? software, double? latitude, double? longitude, string? artist, string? copyright)
             ExtractImageMetadata(Stream imageStream)
    {
        var generalMetadata = new Dictionary<string, string>();
        DateTime? dateTaken = null;
        string? cameraModel = null;
        string? cameraMake = null;
        string? software = null;
        double? latitude = null;
        double? longitude = null;
        string? artist = null;
        string? copyright = null;

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(imageStream);

            foreach (var directory in directories)
            {
                foreach (var tag in directory.Tags)
                {
                    var key = $"{directory.Name}.{tag.Name}";
                    var value = tag.Description ?? "";
                    generalMetadata[key] = value;
                }

                if (directory is ExifIfd0Directory exifIfd0)
                {
                    try
                    {
                        cameraMake = exifIfd0.GetDescription(ExifDirectoryBase.TagMake);
                        cameraModel = exifIfd0.GetDescription(ExifDirectoryBase.TagModel);
                        software = exifIfd0.GetDescription(ExifDirectoryBase.TagSoftware);
                        artist = exifIfd0.GetDescription(ExifDirectoryBase.TagArtist);
                        copyright = exifIfd0.GetDescription(ExifDirectoryBase.TagCopyright);
                    }
                    catch { }
                }

                if (directory is ExifSubIfdDirectory exifSubIfd)
                {
                    try
                    {
                        dateTaken = exifSubIfd.GetDateTime(ExifDirectoryBase.TagDateTimeOriginal);
                    }
                    catch { }
                }

                if (directory.Name != null && directory.Name.Contains("GPS"))
                {
                    try
                    {
                        latitude = directory.TryGetDouble(2, out var lat) ? lat : null;
                        longitude = directory.TryGetDouble(4, out var lon) ? lon : null;
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error extracting image metadata");
        }

        return (generalMetadata, dateTaken, cameraModel, cameraMake, software, latitude, longitude, artist, copyright);
    }

    private double CompareImageMetadata(ImageInfo img1, ImageInfo img2)
    {
        var score = 0.0;
        var factors = 0;

        if (!string.IsNullOrEmpty(img1.CameraMake) && !string.IsNullOrEmpty(img2.CameraMake))
        {
            factors++;
            if (img1.CameraMake == img2.CameraMake)
            {
                score += 0.3;
                if (!string.IsNullOrEmpty(img1.CameraModel) && img1.CameraModel == img2.CameraModel)
                    score += 0.2;
            }
        }

        if (img1.DateTaken.HasValue && img2.DateTaken.HasValue)
        {
            factors++;
            var timeDiff = Math.Abs((img1.DateTaken.Value - img2.DateTaken.Value).TotalHours);
            if (timeDiff < 1) score += 0.4;
            else if (timeDiff < 24) score += 0.2;
            else if (timeDiff < 168) score += 0.1;
        }

        if (img1.Latitude.HasValue && img2.Latitude.HasValue)
        {
            factors++;
            var distance = CalculateGpsDistance(
                img1.Latitude.Value, img1.Longitude ?? 0,
                img2.Latitude.Value, img2.Longitude ?? 0);

            if (distance < 10) score += 0.4;
            else if (distance < 100) score += 0.2;
            else if (distance < 1000) score += 0.1;
        }

        if (!string.IsNullOrEmpty(img1.Software) && !string.IsNullOrEmpty(img2.Software))
        {
            factors++;
            if (img1.Software == img2.Software)
                score += 0.1;
        }

        if (!string.IsNullOrEmpty(img1.Artist) && !string.IsNullOrEmpty(img2.Artist))
        {
            factors++;
            if (img1.Artist == img2.Artist)
                score += 0.3;
        }

        return factors > 0 ? Math.Min(1.0, score) : 0.0;
    }

    private double CalculateGpsDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;

        var φ1 = lat1 * Math.PI / 180;
        var φ2 = lat2 * Math.PI / 180;
        var Δφ = (lat2 - lat1) * Math.PI / 180;
        var Δλ = (lon2 - lon1) * Math.PI / 180;

        var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                Math.Cos(φ1) * Math.Cos(φ2) *
                Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    private List<PlagiarismResult> RunDeepVerification(
        List<PlagiarismResult> flaggedResults,
        List<FileAnalysisData> allFileData,
        PlagiarismAnalysisSettings settings)
    {
        var verifiedResults = new List<PlagiarismResult>();

        foreach (var result in flaggedResults)
        {
            var fileData = allFileData.FirstOrDefault(f => f.FilePath == result.FilePath);
            if (fileData == null)
            {
                verifiedResults.Add(result);
                continue;
            }

            var verifiedMatches = new List<PlagiarismMatch>();

            foreach (var match in result.Matches)
            {
                var comparedFileData = allFileData.FirstOrDefault(f => f.FilePath == match.ComparedToFilePath);
                if (comparedFileData != null)
                {
                    verifiedMatches.Add(match);
                }
            }

            if (verifiedMatches.Any())
            {
                result.Matches = verifiedMatches;
                result.OverallSimilarityScore = verifiedMatches.Max(m => m.SimilarityScore);
                verifiedResults.Add(result);
            }
        }

        return verifiedResults;
    }
}
