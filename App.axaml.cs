using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SchoolOrganizer.ViewModels;
using SchoolOrganizer.Views;
using SchoolOrganizer.Services;
using Serilog;

namespace SchoolOrganizer;

public partial class App : Application
{
    public override void Initialize()
    {
        // Initialize Serilog - only show warnings and errors in console
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning)
            .WriteTo.File("logs/schoolorganizer-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        AvaloniaXamlLoader.Load(this);

        // Initialize theme system
        ThemeManager.Initialize();
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            
            // Always go directly to MainWindow - authentication handled in Student Gallery
            var mainWindow = new MainWindow();
            mainWindow.DataContext = new MainWindowViewModel();
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            Log.Information("Opened MainWindow - authentication handled in Student Gallery.");

            // Auto-open StudentDetailView with first real classroom - DISABLED to show StudentGallery first
            // await OpenFirstRealClassroom();

            // Prevent the app from shutting down when the main window is closed
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow != null)
            {
                desktop.MainWindow.Show();
                desktop.MainWindow.WindowState = WindowState.Normal;
                desktop.MainWindow.Activate();
            }
        }
    }

    private void DarkMode_Click(object? sender, EventArgs e)
    {
        ThemeManager.ApplyTheme(AppTheme.Dark);
    }

    private void LightMode_Click(object? sender, EventArgs e)
    {
        ThemeManager.ApplyTheme(AppTheme.Light);
    }

    private void Exit_Click(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }


    private async Task OpenFirstRealClassroom()
    {
        try
        {
            Log.Information("=== OpenFirstRealClassroom started ===");
            
            // Initialize Google Auth Service
            var authService = new GoogleAuthService();
            Log.Information("GoogleAuthService created, attempting authentication...");
            var isAuthenticated = await authService.CheckAndAuthenticateAsync();
            Log.Information("Authentication result: {IsAuthenticated}", isAuthenticated);
            
            if (!isAuthenticated || authService.ClassroomService == null)
            {
                Log.Warning("Authentication failed or classroom service not available - skipping auto-open of real classroom");
                return;
            }

            // Get the first available classroom
            var classroomService = new ClassroomDataService(authService.ClassroomService);
            var cachedService = new CachedClassroomDataService(classroomService);
            
            var classrooms = await cachedService.GetActiveClassroomsAsync();
            
            if (classrooms.Count == 0)
            {
                Log.Warning("No active classrooms found - skipping auto-open");
                return;
            }

            var firstClassroom = classrooms.First();
            Log.Information("Opening StudentDetailView for first real classroom: {Name} (ID: {Id})", firstClassroom.Name, firstClassroom.Id);

            // Get students for this classroom
            var students = await cachedService.GetStudentsInCourseAsync(firstClassroom.Id);
            
            if (students.Count == 0)
            {
                Log.Warning("No students found in classroom {Name} - skipping auto-open", firstClassroom.Name);
                return;
            }

            // Look for existing downloaded student folders
            var downloadFolderPath = SettingsService.Instance.LoadDownloadFolderPath();
            
            if (string.IsNullOrEmpty(downloadFolderPath) || !Directory.Exists(downloadFolderPath))
            {
                Log.Warning("No download folder found or folder doesn't exist - skipping auto-open");
                return;
            }
            
            Log.Information("Looking for student folders in download directory: {DownloadPath}", downloadFolderPath);
            
            // Find the first student folder that exists and has files
            string? foundStudentFolder = null;
            string? foundStudentName = null;
            
            // First, look for course folders in the download directory
            var courseFolders = Directory.GetDirectories(downloadFolderPath);
            Log.Information("Found {CourseCount} course folders in download directory", courseFolders.Length);
            
            foreach (var courseFolder in courseFolders)
            {
                Log.Information("Checking course folder: {CourseFolder}", Path.GetFileName(courseFolder));
                
                foreach (var student in students)
                {
                    var studentName = student.Profile?.Name?.FullName ?? "Unknown Student";
                    var sanitizedStudentName = SanitizeFolderName(studentName);
                    
                    // Look for student folder inside the course folder
                    var studentFolderPath = Path.Combine(courseFolder, sanitizedStudentName);
                    
                    Log.Information("Checking student: {StudentName} -> {SanitizedName} -> {FolderPath}", studentName, sanitizedStudentName, studentFolderPath);
                    
                    if (Directory.Exists(studentFolderPath))
                    {
                        var fileCount = Directory.GetFiles(studentFolderPath, "*", SearchOption.AllDirectories).Length;
                        Log.Information("Student folder exists with {FileCount} files: {FolderPath}", fileCount, studentFolderPath);
                        
                        if (fileCount > 0)
                        {
                            foundStudentFolder = studentFolderPath;
                            foundStudentName = studentName;
                            Log.Information("Found existing student folder with files: {StudentName} at {FolderPath}", studentName, studentFolderPath);
                            break;
                        }
                    }
                    else
                    {
                        Log.Information("Student folder does not exist: {FolderPath}", studentFolderPath);
                    }
                }
                
                if (foundStudentFolder != null)
                    break;
            }
            
            if (foundStudentFolder == null)
            {
                Log.Warning("No existing student folders with files found - skipping auto-open");
                return;
            }
            
            // Create StudentDetailView for the found student with real files
            var detailViewModel = new StudentDetailViewModel();
            var detailWindow = new Views.AssignmentManagement.AssignmentViewer(detailViewModel);
            
            // Load the real student files using the same method as normal flow
            await detailViewModel.LoadStudentFilesAsync(foundStudentName!, firstClassroom.Name ?? "Unknown Course", foundStudentFolder);
            
            detailWindow.Show();
            Log.Information("Opened StudentDetailView for student: {StudentName} from classroom: {CourseName} with real files", foundStudentName, firstClassroom.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening first real classroom");
        }
    }

    private static string SanitizeFolderName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Unknown";
            
        // Remove invalid characters for folder names
        var sanitized = Regex.Replace(name, @"[<>:""/\\|?*]", "_");
        
        // Replace spaces with underscores
        sanitized = Regex.Replace(sanitized, @"\s+", "_");
        
        // Remove extra underscores and trim
        sanitized = Regex.Replace(sanitized, @"_+", "_").Trim('_');
        
        // Ensure it's not empty after sanitization
        if (string.IsNullOrEmpty(sanitized))
            return "Unknown";
            
        return sanitized;
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseSkia()
            .LogToTrace();
}
