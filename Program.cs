using Avalonia;
using System;
using Splat;

namespace SchoolOrganizer;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Configure Splat to suppress ReactiveUI console logging before app starts
            Locator.CurrentMutable.RegisterConstant(new NullLogger(), typeof(ILogger));
            
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Log the exception but don't try to read from console in GUI app
            System.Diagnostics.Debug.WriteLine($"Application crashed with exception: {ex}");
            // Don't use Console.ReadKey() in GUI applications
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

// NullLogger implementation to suppress all ReactiveUI console logging
public class NullLogger : ILogger
{
    public LogLevel Level => LogLevel.Debug;

    public void Write(string message, LogLevel logLevel)
    {
        // Suppress all ReactiveUI console output
    }

    public void Write(Exception exception, string message, LogLevel logLevel)
    {
        // Suppress all ReactiveUI console output
    }

    public void Write(string message, Type type, LogLevel logLevel)
    {
        // Suppress all ReactiveUI console output
    }

    public void Write(Exception exception, string message, Type type, LogLevel logLevel)
    {
        // Suppress all ReactiveUI console output
    }
}
