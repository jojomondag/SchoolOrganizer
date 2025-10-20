using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SchoolOrganizer.Src.Models.Plagiarism;
using SchoolOrganizer.Src.Services;

namespace SchoolOrganizer.Src.ViewModels;

public partial class ContentViewModel : ObservableObject
{
    private readonly PlagiarismDetectionService _plagiarismService;

    [ObservableProperty]
    private string _courseName = string.Empty;

    [ObservableProperty]
    private string _courseFolder = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "Loading...";

    public ObservableCollection<StudentItem> Students { get; } = new();
    public ObservableCollection<PlagiarismResult> PlagiarismResults { get; } = new();

    public ContentViewModel(string courseName, string courseFolder)
    {
        _plagiarismService = new PlagiarismDetectionService();
        _courseName = courseName;
        _courseFolder = courseFolder;

        // Start loading content asynchronously without blocking UI
        _ = Task.Run(LoadContentAsync);
    }

    private async Task LoadContentAsync()
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = true;
                StatusText = "Loading students...";
            });

            if (!Directory.Exists(CourseFolder))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusText = "Course folder not found";
                    IsLoading = false;
                });
                return;
            }

            // Get student directories in parallel
            var studentDirs = await Task.Run(() => Directory.GetDirectories(CourseFolder));
            Log.Information($"Found {studentDirs.Length} student directories");

            // Process student directories in parallel for better performance
            var studentTasks = studentDirs.Select(async studentDir =>
            {
                var studentName = Path.GetFileName(studentDir);
                
                // Run file system operations in parallel
                var assignmentDirsTask = Task.Run(() => Directory.GetDirectories(studentDir));
                var fileCountTask = Task.Run(() => Directory.GetFiles(studentDir, "*.*", SearchOption.AllDirectories).Length);
                
                await Task.WhenAll(assignmentDirsTask, fileCountTask);
                
                return new StudentItem
                {
                    Name = studentName,
                    FolderPath = studentDir,
                    AssignmentCount = assignmentDirsTask.Result.Length,
                    FileCount = fileCountTask.Result
                };
            });

            var studentItems = await Task.WhenAll(studentTasks);

            // Update UI on the UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Students.Clear();
                foreach (var student in studentItems)
                {
                    Students.Add(student);
                }
                StatusText = $"Loaded {Students.Count} students";
                IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading content");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = $"Error: {ex.Message}";
                IsLoading = false;
            });
        }
    }

    [RelayCommand]
    private async Task RunPlagiarismDetection()
    {
        try
        {
            IsLoading = true;
            StatusText = "Running plagiarism detection...";
            PlagiarismResults.Clear();

            var results = await _plagiarismService.AnalyzeCourseForPlagiarismAsync(CourseFolder);

            foreach (var result in results.OrderByDescending(r => r.OverallSimilarityScore))
            {
                PlagiarismResults.Add(result);
            }

            StatusText = $"Found {PlagiarismResults.Count} potential plagiarism cases";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during plagiarism detection");
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenStudentFolder(StudentItem? student)
    {
        if (student == null || !Directory.Exists(student.FolderPath))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = student.FolderPath,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening student folder");
        }
    }

    [RelayCommand]
    private async Task ViewStudentDetail(StudentItem? student)
    {
        if (student == null)
            return;

        try
        {
            var detailViewModel = new StudentDetailViewModel();
            var detailWindow = new Views.AssignmentManagement.AssignmentViewer(detailViewModel);
            
            // Load the student files asynchronously
            await detailViewModel.LoadStudentFilesAsync(student.Name, CourseName, student.FolderPath);
            
            detailWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening student detail");
        }
    }

}

public class StudentItem
{
    public string Name { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public int AssignmentCount { get; set; }
    public int FileCount { get; set; }
}
