using System;
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

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        // Subscribe to scroll requests from MainWindow
        if (_mainWindowViewModel != null)
        {
            _mainWindowViewModel.AssignmentViewScrollRequested -= OnScrollRequested;
        }

        // Find the MainWindowViewModel
        var window = this.FindAncestorOfType<Window>();
        if (window?.DataContext is MainWindowViewModel mainVM)
        {
            _mainWindowViewModel = mainVM;
            _mainWindowViewModel.AssignmentViewScrollRequested += OnScrollRequested;
        }
    }

    private void OnScrollRequested(object? sender, string assignmentName)
    {
        Log.Information("Scroll requested for assignment: {AssignmentName}", assignmentName);
        
        // Forward the scroll request to the AssignmentViewControl
        var control = this.FindControl<AssignmentViewControl>("AssignmentControl");
        control?.ScrollToAssignment(assignmentName);
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

