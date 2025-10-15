using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SchoolOrganizer.Src.Models.Plagiarism;

public class PlagiarismResult
{
    public string StudentName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public List<PlagiarismMatch> Matches { get; set; } = new();
    public double OverallSimilarityScore { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public string AssignmentName { get; set; } = string.Empty;

    public bool HasSuspiciousActivity => OverallSimilarityScore > 0.7 || Matches.Any(m => m.SimilarityScore > 0.8);
    public string SeverityLevel => OverallSimilarityScore switch
    {
        >= 0.9 => "High",
        >= 0.7 => "Medium",
        >= 0.5 => "Low",
        _ => "Minimal"
    };
}

public class PlagiarismMatch
{
    public string ComparedToStudentName { get; set; } = string.Empty;
    public string ComparedToFileName { get; set; } = string.Empty;
    public string ComparedToFilePath { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public List<SimilarityIndicator> Indicators { get; set; } = new();
    public string MatchType { get; set; } = string.Empty;
    public string MatchId { get; set; } = string.Empty;
    public bool IsReciprocalMatch { get; set; } = false;
}

public class SimilarityIndicator
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Evidence { get; set; } = string.Empty;
}

public class FileAnalysisData
{
    public string FilePath { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string AssignmentName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Hash { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string FileExtension { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> TextLines { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public int WordCount { get; set; }
    public int CharacterCount { get; set; }
    public List<string> UniqueWords { get; set; } = new();
    public List<ImageInfo> Images { get; set; } = new();
    public string StructuralSignature { get; set; } = string.Empty;
}

public class ImageInfo
{
    public string ImageHash { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public long Size { get; set; }
    public string Format { get; set; } = string.Empty;
    public string PerceptualHash { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime? DateTaken { get; set; }
    public string? CameraModel { get; set; }
    public string? CameraMake { get; set; }
    public string? Software { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Artist { get; set; }
    public string? Copyright { get; set; }
}

public class PlagiarismAnalysisSettings
{
    public double ContentSimilarityThreshold { get; set; } = 0.7;
    public double FileSizeTolerancePercent { get; set; } = 0.05;
    public bool AnalyzeTextContent { get; set; } = true;
    public bool AnalyzeFileMetadata { get; set; } = true;
    public bool AnalyzeFileSize { get; set; } = true;
    public bool AnalyzeFileHashes { get; set; } = true;
    public bool AnalyzeImages { get; set; } = true;
    public bool AnalyzeStructuralSimilarity { get; set; } = true;
    public List<string> SupportedFileExtensions { get; set; } = new()
    {
        ".txt", ".docx", ".pdf", ".html", ".md", ".rtf", ".odt"
    };
    public int MinWordCountForAnalysis { get; set; } = 10;
    public int MaxFileSizeForTextAnalysis { get; set; } = 1024 * 1024;
    public int MaxCharactersForTextComparison { get; set; } = 50000;
    public int ComparisonTimeoutSeconds { get; set; } = 10;
    public bool IgnoreCommonWords { get; set; } = true;
    public List<string> CommonWordsToIgnore { get; set; } = new()
    {
        "and", "the", "a", "an", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "from",
        "up", "about", "into", "through", "during", "before", "after", "above", "below", "between"
    };
}

public interface IPlagiarismDetectionService
{
    Task<List<PlagiarismResult>> AnalyzeCourseForPlagiarismAsync(string courseDirectory, PlagiarismAnalysisSettings? settings = null);
    Task<List<PlagiarismResult>> AnalyzeAssignmentForPlagiarismAsync(string assignmentDirectory, PlagiarismAnalysisSettings? settings = null);
    Task<PlagiarismMatch> CompareFilesAsync(string file1Path, string file2Path, PlagiarismAnalysisSettings? settings = null);
    Task<FileAnalysisData?> ExtractFileDataAsync(string filePath);
    double CalculateTextSimilarity(string content1, string content2);
    Task<string> CalculateFileHashAsync(string filePath);
}
