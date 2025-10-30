using System;

namespace SchoolOrganizer.Src.Services.Downloads.Models;

public record DownloadedFileInfo(string FileId, string FileName, string LocalPath, DateTime DownloadDateTime, string StudentName, string AssignmentName, bool IsLink);

