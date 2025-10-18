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