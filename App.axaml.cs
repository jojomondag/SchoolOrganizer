using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using Avalonia.Markup.Xaml;
using SchoolOrganizer.Src.ViewModels;
using SchoolOrganizer.Src.Views;
using SchoolOrganizer.Src.Services;
using Serilog;

namespace SchoolOrganizer;

public partial class App : Application
{

    public override void Initialize()
    {
        // Initialize Serilog - show information and above in console, reduce file logging
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
            .WriteTo.File("logs/schoolorganizer-.txt", 
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
            .CreateLogger();

        AvaloniaXamlLoader.Load(this);

        // Initialize theme system
        ThemeManager.Initialize();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            
            // Add global exception handler
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                Log.Fatal(exception, "UNHANDLED EXCEPTION - IsTerminating: {IsTerminating}", e.IsTerminating);
                Log.Fatal("Exception Type: {ExceptionType}", exception?.GetType().FullName);
                Log.Fatal("Stack Trace: {StackTrace}", exception?.StackTrace);
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Log.Fatal(e.Exception, "UNOBSERVED TASK EXCEPTION");
                e.SetObserved(); // Prevent process termination
            };
            
            // Main application window
            var mainWindow = new MainWindow();
            mainWindow.DataContext = new MainWindowViewModel();
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            Log.Information("Opened MainWindow - authentication handled in Student Gallery.");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var coordinator = SchoolOrganizer.Src.Services.AssignmentViewCoordinator.Instance;
            var main = desktop.MainWindow;
            var detached = coordinator.GetDetachedWindow();

            bool mainVisible = main?.IsVisible == true;
            bool detachedVisible = coordinator.IsDetached && detached != null && detached.IsVisible;

            // If both are visible → Hide both. Else → Show available windows.
            if (mainVisible && detachedVisible)
            {
                main?.Hide();
                detached?.Hide();
            }
            else
            {
                if (main != null)
                {
                    main.Show();
                    if (main.WindowState == WindowState.Minimized)
                        main.WindowState = WindowState.Normal;
                    main.Activate();
                }

                if (coordinator.IsDetached && detached != null)
                {
                    if (!detached.IsVisible)
                        detached.Show();
                    if (detached.WindowState == WindowState.Minimized)
                        detached.WindowState = WindowState.Normal;
                    detached.Activate();
                }
            }

            // Update the menu item header if the sender is the menu item
            if (sender is Avalonia.Controls.NativeMenuItem menuItem)
            {
                // Re-evaluate visibility after action
                mainVisible = desktop.MainWindow?.IsVisible == true;
                detached = coordinator.GetDetachedWindow();
                detachedVisible = coordinator.IsDetached && detached != null && detached.IsVisible;
                menuItem.Header = (mainVisible && detachedVisible) ? "Hide" : "Show";
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