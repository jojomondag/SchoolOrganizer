using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Apis.Classroom.v1.Data;
using ReactiveUI;
using Serilog;
using SchoolOrganizer.Models;
using SchoolOrganizer.Services;
using SchoolOrganizer.Services.Utilities;

namespace SchoolOrganizer.ViewModels;

public partial class ContentViewModel : ObservableObject
{
    private readonly PlagiarismDetectionService _plagiarismService;
    private readonly Action? _closeAction;

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

    public ContentViewModel(string courseName, string courseFolder, Action? closeAction = null)
    {
        _plagiarismService = new PlagiarismDetectionService();
        _courseName = courseName;
        _courseFolder = courseFolder;
        _closeAction = closeAction;

        Task.Run(LoadContentAsync);
    }

    private Task LoadContentAsync()
    {
        try
        {
            IsLoading = true;
            StatusText = "Loading students...";

            if (!Directory.Exists(CourseFolder))
            {
                StatusText = "Course folder not found";
                return Task.CompletedTask;
            }

            var studentDirs = Directory.GetDirectories(CourseFolder);
            Log.Information($"Found {studentDirs.Length} student directories");

            foreach (var studentDir in studentDirs)
            {
                var studentName = Path.GetFileName(studentDir);
                var assignmentDirs = Directory.GetDirectories(studentDir);
                var fileCount = Directory.GetFiles(studentDir, "*.*", SearchOption.AllDirectories).Length;

                Students.Add(new StudentItem
                {
                    Name = studentName,
                    FolderPath = studentDir,
                    AssignmentCount = assignmentDirs.Length,
                    FileCount = fileCount
                });
            }

            StatusText = $"Loaded {Students.Count} students";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading content");
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
        return Task.CompletedTask;
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
            var detailWindow = new Views.ContentView.StudentDetailView(detailViewModel);
            
            // Load the student files asynchronously
            await detailViewModel.LoadStudentFilesAsync(student.Name, CourseName, student.FolderPath);
            
            detailWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening student detail");
        }
    }

    [RelayCommand]
    private void Close()
    {
        _closeAction?.Invoke();
    }
}

public class StudentItem
{
    public string Name { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public int AssignmentCount { get; set; }
    public int FileCount { get; set; }
}
