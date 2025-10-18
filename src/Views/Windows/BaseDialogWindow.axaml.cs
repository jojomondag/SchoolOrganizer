using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Threading.Tasks;

namespace SchoolOrganizer.Src.Views.Windows;

/// <summary>
/// Base class for dialog windows with common functionality
/// </summary>
public abstract class BaseDialogWindow : Window
{
    protected BaseDialogWindow()
    {
        // Common window setup
        KeyDown += OnKeyDown;
        Loaded += OnWindowLoaded;
    }

    /// <summary>
    /// Handle Escape key to close window
    /// </summary>
    protected virtual void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Called when window is loaded - override for specific initialization
    /// </summary>
    protected virtual void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        // Override in derived classes for specific initialization
    }

    /// <summary>
    /// Show dialog and return result
    /// </summary>
    public virtual async Task<T?> ShowDialogAsync<T>(Window parent) where T : class
    {
        await ShowDialog(parent);
        return GetResult<T>();
    }

    /// <summary>
    /// Get the result from the dialog - override in derived classes
    /// </summary>
    protected virtual T? GetResult<T>() where T : class
    {
        return null;
    }
}

/// <summary>
/// Base class for windows that return a simple string result
/// </summary>
public abstract class BaseStringResultWindow : BaseDialogWindow
{
    public string? Result { get; protected set; }

    protected override T? GetResult<T>() where T : class
    {
        if (typeof(T) == typeof(string))
        {
            return Result as T;
        }
        return null;
    }

    public static async Task<string?> ShowAsync<T>(Window parent) where T : BaseStringResultWindow, new()
    {
        var dialog = new T();
        await dialog.ShowDialog(parent);
        return dialog.Result;
    }
}
