using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using SchoolOrganizer.Src.Services;
using SchoolOrganizer.Src.ViewModels;
using Serilog;

namespace SchoolOrganizer.Src.Views.AssignmentManagement;

/// <summary>
/// View wrapper for the AssignmentViewControl when embedded in the main window
/// Includes header bar with student info and detach button
/// </summary>
public partial class EmbeddedAssignmentView : UserControl
{
    private MainWindowViewModel? _mainWindowViewModel;

    public EmbeddedAssignmentView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Log.Information("EmbeddedAssignmentView attached to visual tree");
        SubscribeToMainWindowEvents();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Log.Information("EmbeddedAssignmentView detached from visual tree");
        UnsubscribeFromMainWindowEvents();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Log.Information("EmbeddedAssignmentView DataContext changed: {Type}", DataContext?.GetType().Name ?? "null");
        
        // Try to subscribe when DataContext changes (in case we're already attached)
        SubscribeToMainWindowEvents();
    }

    private void SubscribeToMainWindowEvents()
    {
        Log.Information("SubscribeToMainWindowEvents called");
        
        // Unsubscribe first to avoid duplicates
        UnsubscribeFromMainWindowEvents();

        // Find the MainWindowViewModel
        var window = this.FindAncestorOfType<Window>();
        if (window?.DataContext is MainWindowViewModel mainVM)
        {
            _mainWindowViewModel = mainVM;
            _mainWindowViewModel.AssignmentViewScrollRequested += OnScrollRequested;
            Log.Information("Successfully subscribed to AssignmentViewScrollRequested event from MainWindowViewModel");
        }
        else
        {
            Log.Warning("SubscribeToMainWindowEvents - Could not find MainWindowViewModel. Window: {Window}, DataContext: {DataContext}",
                window?.GetType().Name ?? "null", window?.DataContext?.GetType().Name ?? "null");
        }
    }

    private void UnsubscribeFromMainWindowEvents()
    {
        if (_mainWindowViewModel != null)
        {
            _mainWindowViewModel.AssignmentViewScrollRequested -= OnScrollRequested;
            Log.Information("Unsubscribed from AssignmentViewScrollRequested event");
            _mainWindowViewModel = null;
        }
    }

    private void OnScrollRequested(object? sender, string assignmentName)
    {
        Log.Information("Scroll requested for assignment: {AssignmentName}", assignmentName);
        
        // Forward the scroll request to the AssignmentViewControl
        var control = this.FindControl<AssignmentViewControl>("AssignmentControl");
        if (control == null)
        {
            Log.Warning("OnScrollRequested - AssignmentControl not found. Trying to find by type...");
            // Try to find by type as fallback
            control = this.GetVisualDescendants().OfType<AssignmentViewControl>().FirstOrDefault();
            if (control == null)
            {
                Log.Error("OnScrollRequested - Could not find AssignmentViewControl instance");
                return;
            }
        }
        
        Log.Information("OnScrollRequested - Found AssignmentViewControl, calling ScrollToAssignment");
        control.ScrollToAssignment(assignmentName);
    }

    /// <summary>
    /// Handles the detach button click - moves the view to a separate window
    /// </summary>
    private void OnDetachClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Detach button clicked in embedded view");
            
            // Use the coordinator to handle the detach operation
            AssignmentViewCoordinator.Instance.DetachFromEmbedded();
            
            Log.Information("Successfully initiated detach from embedded view");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error detaching assignment view");
        }
    }
}

