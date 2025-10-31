using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SchoolOrganizer.Src.ViewModels;
using SchoolOrganizer.Src.Models.Assignments;
using SchoolOrganizer.Src.Views.Components;
using Serilog;

namespace SchoolOrganizer.Src.Views.AssignmentManagement;

/// <summary>
/// Reusable control for displaying a student's downloaded assignments
/// Can be used in both embedded mode (main window) and detached mode (separate window)
/// </summary>
public partial class AssignmentViewControl : UserControl
{
    private Dictionary<string, DispatcherTimer> _notesAutoSaveTimers = new();
    private Dictionary<string, DispatcherTimer> _widthAutoSaveTimers = new();
    private Dictionary<string, bool> _notesJustOpened = new(); // Track when notes were just opened

    /// <summary>
    /// Property to control whether the internal sidebar is shown
    /// </summary>
    public static readonly StyledProperty<bool> ShowInternalSidebarProperty =
        AvaloniaProperty.Register<AssignmentViewControl, bool>(nameof(ShowInternalSidebar), defaultValue: true);

    public bool ShowInternalSidebar
    {
        get => GetValue(ShowInternalSidebarProperty);
        set => SetValue(ShowInternalSidebarProperty, value);
    }

    public AssignmentViewControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ShowInternalSidebarProperty)
        {
            UpdateSidebarVisibility();
        }
    }

    private void UpdateSidebarVisibility()
    {
        var sidebarBorder = this.FindControl<Border>("SidebarBorder");
        var splitView = this.FindControl<SplitView>("MainSplitView");
        
        if (sidebarBorder != null)
        {
            sidebarBorder.IsVisible = ShowInternalSidebar;
        }
        
        if (splitView != null)
        {
            // When hiding the sidebar, collapse the pane completely
            if (!ShowInternalSidebar)
            {
                splitView.CompactPaneLength = 0;
                splitView.OpenPaneLength = 0;
                splitView.IsPaneOpen = false;
            }
            else
            {
                splitView.CompactPaneLength = 56;
                splitView.OpenPaneLength = 250;
            }
            
            Log.Information("Updated sidebar visibility to: {IsVisible}, CompactPaneLength: {CompactLength}, OpenPaneLength: {OpenLength}", 
                ShowInternalSidebar, splitView.CompactPaneLength, splitView.OpenPaneLength);
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        SubscribeToFilePreviewEvents();
        SubscribeToAssignmentGroupEvents();
    }

    /// <summary>
    /// Subscribes to FilePreviewControl events by using a routed event approach
    /// </summary>
    private void SubscribeToFilePreviewEvents()
    {
        Log.Information("Subscribing to FilePreviewControl.MaximizeClickedEvent");
        // Remove any existing handlers first to prevent duplicates
        this.RemoveHandler(FilePreviewControl.MaximizeClickedEvent, OnFilePreviewMaximizeClicked);
        this.AddHandler(FilePreviewControl.MaximizeClickedEvent, OnFilePreviewMaximizeClicked);
        Log.Information("Successfully subscribed to MaximizeClickedEvent");
    }

    /// <summary>
    /// Subscribes to AssignmentGroup PropertyChanged events to detect width changes
    /// </summary>
    private void SubscribeToAssignmentGroupEvents()
    {
        if (DataContext is not StudentDetailViewModel viewModel)
            return;

        // Subscribe to PropertyChanged events on each AssignmentGroup
        foreach (var assignmentGroup in viewModel.AllFilesGrouped)
        {
            // Remove existing handler to prevent duplicates
            assignmentGroup.PropertyChanged -= OnAssignmentGroupPropertyChanged;
            assignmentGroup.PropertyChanged += OnAssignmentGroupPropertyChanged;
        }

        // Set initial column widths for notes sidebars after a delay to ensure visual tree is loaded
        _ = Task.Delay(100).ContinueWith(_ =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateAllNotesColumnWidths();
            });
        });

        Log.Information("Subscribed to {Count} AssignmentGroup PropertyChanged events", viewModel.AllFilesGrouped.Count);
    }

    /// <summary>
    /// Updates all notes sidebar column widths based on saved values
    /// </summary>
    private void UpdateAllNotesColumnWidths()
    {
        try
        {
            // Find all AssignmentContentGrid instances and update their column widths
            var mainContentPanel = this.FindControl<Border>("MainContentPanel");
            if (mainContentPanel == null)
            {
                Log.Warning("MainContentPanel not found");
                return;
            }

            var grids = mainContentPanel.GetVisualDescendants()
                .OfType<Grid>()
                .Where(g => g.Name == "AssignmentContentGrid")
                .ToList();

            Log.Information("Found {Count} AssignmentContentGrid instances", grids.Count);

            if (DataContext is not StudentDetailViewModel viewModel)
            {
                Log.Warning("DataContext is not StudentDetailViewModel");
                return;
            }

            int updated = 0;
            foreach (var grid in grids)
            {
                // Find the corresponding assignment group by checking the grid's DataContext
                if (grid.DataContext is AssignmentGroup assignmentGroup && grid.ColumnDefinitions.Count >= 3)
                {
                    var columnDef = grid.ColumnDefinitions[2];

                    // If notes are expanded, use the saved width; otherwise collapse to 0
                    if (assignmentGroup.IsNotesExpanded)
                    {
                        // Use saved width, or default to 220 if too small
                        var width = assignmentGroup.NotesSidebarWidth;
                        if (width < 150)
                        {
                            width = 220; // Use default width if saved width is too small
                            assignmentGroup.NotesSidebarWidth = width; // Update the saved width
                        }
                        else
                        {
                            width = Math.Min(600, width); // Clamp to max
                        }

                        columnDef.Width = new GridLength(width, GridUnitType.Pixel);
                        columnDef.MinWidth = 150;
                        columnDef.MaxWidth = 600;
                        Log.Information("Set column width for {Assignment} to {Width} (expanded)",
                            assignmentGroup.AssignmentName, width);
                    }
                    else
                    {
                        columnDef.Width = new GridLength(0, GridUnitType.Pixel);
                        columnDef.MinWidth = 0;
                        columnDef.MaxWidth = double.PositiveInfinity;
                        Log.Information("Set column width for {Assignment} to 0 (collapsed)",
                            assignmentGroup.AssignmentName);
                    }
                    updated++;
                }
                else
                {
                    Log.Warning("Grid DataContext is not AssignmentGroup or doesn't have 3 columns. DataContext type: {Type}",
                        grid.DataContext?.GetType().Name ?? "null");
                }
            }

            Log.Information("Updated {UpdatedCount} out of {TotalCount} grids", updated, grids.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating notes column widths");
        }
    }

    /// <summary>
    /// Handles PropertyChanged events from AssignmentGroup (reserved for future use)
    /// </summary>
    private void OnAssignmentGroupPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Currently not handling any property changes here
        // Width changes are handled via GridSplitter DragCompleted event
    }

    /// <summary>
    /// Handles GridSplitter drag completed to save the new sidebar width
    /// </summary>
    private async void OnNotesSplitterDragCompleted(object? sender, Avalonia.Input.VectorEventArgs e)
    {
        try
        {
            if (sender is not GridSplitter splitter)
                return;

            // Find the parent Grid (AssignmentContentGrid)
            var grid = splitter.GetVisualAncestors().OfType<Grid>().FirstOrDefault(g => g.Name == "AssignmentContentGrid");
            if (grid == null || grid.ColumnDefinitions.Count < 3)
            {
                Log.Warning("Could not find parent grid or grid doesn't have enough columns");
                return;
            }

            // Get the assignment group from the grid's DataContext
            if (grid.DataContext is not AssignmentGroup assignmentGroup)
            {
                Log.Warning("Grid DataContext is not an AssignmentGroup");
                return;
            }

            // Get the new width from the column definition
            var columnDef = grid.ColumnDefinitions[2];
            var newWidth = columnDef.Width.Value;

            // Clamp to valid range (150-600)
            newWidth = Math.Max(150, Math.Min(600, newWidth));

            Log.Information("GridSplitter drag completed for {Assignment}, new width: {Width}",
                assignmentGroup.AssignmentName, newWidth);

            // Update the assignment group's width property
            assignmentGroup.NotesSidebarWidth = newWidth;

            // Save to student data
            if (DataContext is StudentDetailViewModel viewModel)
            {
                await viewModel.SaveAssignmentNotesSidebarWidthAsync(assignmentGroup.AssignmentName, newWidth);
                Log.Information("Saved notes sidebar width for {Assignment}", assignmentGroup.AssignmentName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling notes splitter drag completed");
        }
    }

    /// <summary>
    /// Handles summary button click - scrolls to summary section
    /// </summary>
    private void OnSummaryClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("OnSummaryClick called");
            
            // Find the main summary section (below assignments)
            var summarySection = this.FindControl<Border>("SummarySectionMain");
            if (summarySection != null)
            {
                // Find the ScrollViewer in the main content area
                var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
                if (scrollViewer != null)
                {
                    // Scroll to the summary section
                    summarySection.BringIntoView();
                    Log.Information("Scrolled to summary section");
                }
                else
                {
                    Log.Warning("Could not find MainScrollViewer to scroll to summary");
                }
            }
            else
            {
                Log.Warning("Could not find SummarySectionMain to scroll to");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error scrolling to summary section");
        }
    }

    /// <summary>
    /// Handles assignment navigation button clicks
    /// </summary>
    private void OnAssignmentClick(object sender, RoutedEventArgs e)
    {
        Log.Information("OnAssignmentClick called - Sender: {SenderType}", sender?.GetType().Name);
        ToggleAssignmentExpansion(sender);
    }

    /// <summary>
    /// Handles assignment navigation button pointer pressed events (fallback if Click doesn't work)
    /// </summary>
    private void OnAssignmentPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Log.Information("OnAssignmentPointerPressed called - Sender: {SenderType}", sender?.GetType().Name);
        
        // Only handle left mouse button clicks
        if (sender is Visual visual && e.GetCurrentPoint(visual).Properties.IsLeftButtonPressed)
        {
            ToggleAssignmentExpansion(sender);
            e.Handled = true; // Prevent event bubbling
        }
    }

    /// <summary>
    /// Common logic to toggle assignment expansion
    /// </summary>
    private void ToggleAssignmentExpansion(object? sender)
    {
        try
        {
            Log.Information("ToggleAssignmentExpansion called - Sender: {SenderType}", sender?.GetType().Name);

            if (sender is not Button button)
            {
                Log.Warning("ToggleAssignmentExpansion - Invalid sender. Sender: {SenderType}",
                    sender?.GetType().Name);
                return;
            }

            // The button's DataContext is the AssignmentGroup instance from the DataTemplate
            // Since both ItemsControls bind to the same AllFilesGrouped collection,
            // modifying this instance will update both views
            AssignmentGroup? assignmentGroup = null;
            
            if (button.DataContext is AssignmentGroup directAssignmentGroup)
            {
                assignmentGroup = directAssignmentGroup;
            }
            else
            {
                Log.Warning("ToggleAssignmentExpansion - Button DataContext is not AssignmentGroup. DataContext: {DataContextType}, Tag: {Tag}",
                    button.DataContext?.GetType().Name ?? "null", button.Tag?.ToString() ?? "null");
                
                // Fallback: try to get from Tag
                if (button.Tag is string tagAssignmentName && DataContext is StudentDetailViewModel fallbackViewModel)
                {
                    assignmentGroup = fallbackViewModel.AllFilesGrouped.FirstOrDefault(ag => ag.AssignmentName == tagAssignmentName);
                    if (assignmentGroup == null)
                    {
                        Log.Warning("ToggleAssignmentExpansion - Assignment not found in AllFilesGrouped: {AssignmentName}", tagAssignmentName);
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            var assignmentName = assignmentGroup.AssignmentName;
            var currentExpanded = assignmentGroup.IsExpanded;

            Log.Information("ToggleAssignmentExpansion - Assignment: {AssignmentName}, Current IsExpanded: {IsExpanded}",
                assignmentName, currentExpanded);

            if (DataContext is not StudentDetailViewModel viewModel)
            {
                Log.Warning("ToggleAssignmentExpansion - Control DataContext is not StudentDetailViewModel");
                return;
            }

            // Ensure navigation sidebar is open so user can see which assignment they clicked
            if (!viewModel.IsNavigationOpen)
            {
                viewModel.IsNavigationOpen = true;
            }

            // Implement accordion behavior: if expanding, collapse all other assignments
            if (!currentExpanded)
            {
                // Collapse all other assignments
                if (viewModel.AllFilesGrouped != null)
                {
                    foreach (var otherGroup in viewModel.AllFilesGrouped)
                    {
                        if (otherGroup != assignmentGroup && otherGroup.IsExpanded)
                        {
                            otherGroup.IsExpanded = false;
                            Log.Information("ToggleAssignmentExpansion - Collapsed other assignment: {AssignmentName}", otherGroup.AssignmentName);
                        }
                    }
                }
                
                // Expand this assignment and auto-expand all its files
                assignmentGroup.IsExpanded = true;
                foreach (var file in assignmentGroup.Files)
                {
                    // Auto-expand files that have code or text content
                    if (file.IsCode || file.IsText)
                    {
                        file.IsExpanded = true;
                    }
                }
                
                Log.Information("ToggleAssignmentExpansion - Expanded assignment: {AssignmentName} and auto-expanded {FileCount} files", 
                    assignmentName, assignmentGroup.Files.Count(f => f.IsCode || f.IsText));
            }
            else
            {
                // Collapse this assignment
                assignmentGroup.IsExpanded = false;
                Log.Information("ToggleAssignmentExpansion - Collapsed assignment: {AssignmentName}", assignmentName);
            }

            // Scroll to the assignment after a short delay to allow layout to update
            // Only scroll if expanding (if collapsing, we don't need to scroll)
            if (assignmentGroup.IsExpanded)
            {
                _ = Task.Delay(100).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ScrollToAssignmentAtTop(assignmentName);
                    });
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error toggling assignment expansion - Sender: {SenderType}", sender?.GetType().Name);
        }
    }

    /// <summary>
    /// Scrolls to a specific assignment and positions it at the top of the viewport
    /// </summary>
    private void ScrollToAssignmentAtTop(string assignmentName)
    {
        try
        {
            Log.Information("Scrolling to assignment at top: {AssignmentName}", assignmentName);

            var mainScrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
            if (mainScrollViewer == null)
            {
                Log.Warning("MainScrollViewer not found");
                return;
            }

            // Find the assignment header with this name
            var headers = mainScrollViewer.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.Name == "AssignmentHeaderBorder")
                .ToList();

            var targetHeader = headers.FirstOrDefault(h =>
                h.DataContext is AssignmentGroup ag && ag.AssignmentName == assignmentName);

            if (targetHeader == null)
            {
                Log.Warning("Assignment header not found: {AssignmentName}", assignmentName);
                return;
            }

            // Wait for layout to update before scrolling
            EventHandler? layoutHandler = null;
            var startTime = DateTime.Now;

            layoutHandler = (s, e) =>
            {
                // Timeout protection
                if ((DateTime.Now - startTime).TotalMilliseconds > 2000)
                {
                    mainScrollViewer.LayoutUpdated -= layoutHandler;
                    Log.Warning("Layout update timeout, aborting scroll");
                    return;
                }

                // Unsubscribe immediately to run only once
                mainScrollViewer.LayoutUpdated -= layoutHandler;

                // Find the ItemsControl container
                var itemsControl = mainScrollViewer.GetVisualDescendants().OfType<ItemsControl>().FirstOrDefault();
                if (itemsControl == null)
                {
                    Log.Warning("Could not find ItemsControl");
                    return;
                }

                // Calculate the absolute position of the header relative to the ItemsControl
                var absolutePoint = targetHeader.TranslatePoint(new Point(0, 0), itemsControl);
                if (absolutePoint.HasValue)
                {
                    var targetY = absolutePoint.Value.Y;

                    // Calculate max scroll position
                    var extent = mainScrollViewer.Extent;
                    var viewport = mainScrollViewer.Viewport;
                    var maxScrollY = Math.Max(0, extent.Height - viewport.Height);

                    // Scroll to position the header at the top
                    var optimalY = Math.Min(targetY, maxScrollY);
                    var currentOffset = mainScrollViewer.Offset;

                    mainScrollViewer.Offset = new Vector(currentOffset.X, optimalY);

                    Log.Information("Scrolled to assignment {AssignmentName} at top (Y: {Y})", assignmentName, optimalY);
                }
                else
                {
                    Log.Warning("Could not calculate position for assignment header");
                    // Fallback to BringIntoView
                    targetHeader.BringIntoView(new Rect(0, 0, 100, 100));
                }
            };

            mainScrollViewer.LayoutUpdated += layoutHandler;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error scrolling to assignment at top: {AssignmentName}", assignmentName);
        }
    }

    /// <summary>
    /// Handles assignment header click to toggle expansion
    /// </summary>
    private void OnAssignmentHeaderClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button button)
            {
                Log.Warning("OnAssignmentHeaderClick - sender is not a Button");
                return;
            }

            // Get the assignment from the DataContext
            // First try the button's DataContext, then check the parent Border
            AssignmentGroup? assignmentGroup = null;

            if (button.DataContext is AssignmentGroup group)
            {
                assignmentGroup = group;
            }
            else
            {
                // Try to find the AssignmentGroup from the parent Border
                var parentBorder = button.GetVisualAncestors()
                    .OfType<Border>()
                    .FirstOrDefault(b => b.Name == "AssignmentHeaderBorder");
                
                if (parentBorder?.DataContext is AssignmentGroup borderGroup)
                {
                    assignmentGroup = borderGroup;
                }
                else
                {
                    // Try to find from the parent Grid
                    var parentGrid = button.GetVisualAncestors()
                        .OfType<Grid>()
                        .FirstOrDefault();
                    
                    if (parentGrid?.DataContext is AssignmentGroup gridGroup)
                    {
                        assignmentGroup = gridGroup;
                    }
                }
            }

            if (assignmentGroup == null)
            {
                Log.Warning("OnAssignmentHeaderClick - Could not find AssignmentGroup in DataContext");
                return;
            }

            if (DataContext is not StudentDetailViewModel viewModel)
            {
                Log.Warning("OnAssignmentHeaderClick - DataContext is not StudentDetailViewModel");
                return;
            }

            // Toggle this assignment (allow multiple assignments to be expanded in main view)
            var currentExpanded = assignmentGroup.IsExpanded;
            
            if (!currentExpanded)
            {
                // Expand this assignment and auto-expand all its files
                assignmentGroup.IsExpanded = true;
                foreach (var file in assignmentGroup.Files)
                {
                    // Auto-expand files that have code or text content
                    if (file.IsCode || file.IsText)
                    {
                        file.IsExpanded = true;
                    }
                }
                
                Log.Information("OnAssignmentHeaderClick - Expanded assignment: {AssignmentName} and auto-expanded {FileCount} files", 
                    assignmentGroup.AssignmentName, assignmentGroup.Files.Count(f => f.IsCode || f.IsText));
            }
            else
            {
                // Collapse this assignment
                assignmentGroup.IsExpanded = false;
                Log.Information("OnAssignmentHeaderClick - Collapsed assignment: {AssignmentName}", assignmentGroup.AssignmentName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling assignment header click");
        }
    }

    /// <summary>
    /// Public method to scroll to a specific assignment by name and toggle its expansion
    /// </summary>
    public void ScrollToAssignment(string assignmentName)
    {
        try
        {
            Log.Information("ScrollToAssignment called for: {AssignmentName}", assignmentName);

            // Wait a moment for DataContext to be set if it's not already
            StudentDetailViewModel? viewModel = null;
            
            if (DataContext is StudentDetailViewModel directVM)
            {
                viewModel = directVM;
            }
            else
            {
                Log.Warning("ScrollToAssignment - DataContext is not StudentDetailViewModel. DataContext type: {Type}", 
                    DataContext?.GetType().Name ?? "null");
                
                // Try to get DataContext from parent
                var parent = this.GetVisualParent();
                while (parent != null && viewModel == null)
                {
                    if (parent.DataContext is StudentDetailViewModel parentVM)
                    {
                        viewModel = parentVM;
                        Log.Information("ScrollToAssignment - Found StudentDetailViewModel in parent: {ParentType}", parent.GetType().Name);
                        break;
                    }
                    parent = parent.GetVisualParent();
                }
                
                if (viewModel == null)
                {
                    Log.Error("ScrollToAssignment - Could not find StudentDetailViewModel in DataContext or parent hierarchy");
                    return;
                }
            }

            // Find the assignment group and toggle its expansion
            var assignmentGroup = viewModel.AllFilesGrouped?.FirstOrDefault(ag => ag.AssignmentName == assignmentName);
            if (assignmentGroup == null)
            {
                Log.Warning("ScrollToAssignment - Assignment not found: {AssignmentName}. Total assignments: {Count}", 
                    assignmentName, viewModel.AllFilesGrouped?.Count ?? 0);
                return;
            }

            // Implement accordion behavior: if expanding, collapse all other assignments
            var wasExpanded = assignmentGroup.IsExpanded;
            
            if (!wasExpanded)
            {
                // Collapse all other assignments
                if (viewModel.AllFilesGrouped != null)
                {
                    foreach (var otherGroup in viewModel.AllFilesGrouped)
                    {
                        if (otherGroup != assignmentGroup && otherGroup.IsExpanded)
                        {
                            otherGroup.IsExpanded = false;
                            Log.Information("ScrollToAssignment - Collapsed other assignment: {AssignmentName}", otherGroup.AssignmentName);
                        }
                    }
                }
                
                // Expand this assignment and auto-expand all its files
                assignmentGroup.IsExpanded = true;
                foreach (var file in assignmentGroup.Files)
                {
                    // Auto-expand files that have code or text content
                    if (file.IsCode || file.IsText)
                    {
                        file.IsExpanded = true;
                    }
                }
                
                Log.Information("ScrollToAssignment - Expanded assignment: {AssignmentName} and auto-expanded {FileCount} files", 
                    assignmentName, assignmentGroup.Files.Count(f => f.IsCode || f.IsText));
            }
            else
            {
                // Collapse this assignment
                assignmentGroup.IsExpanded = false;
                Log.Information("ScrollToAssignment - Collapsed assignment: {AssignmentName}", assignmentName);
            }

            // Scroll to the assignment after a short delay to allow layout to update
            _ = Task.Delay(100).ContinueWith(_ =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ScrollToAssignmentAtTop(assignmentName);
                });
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error scrolling to assignment: {AssignmentName}", assignmentName);
        }
    }


    /// <summary>
    /// Handles toggle navigation button clicks
    /// </summary>
    private void OnToggleNavigationClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("OnToggleNavigationClick called - Sender: {SenderType}", sender?.GetType().Name);
            
            if (DataContext is StudentDetailViewModel viewModel)
            {
                Log.Information("Toggling navigation - Current state: {IsOpen}", viewModel.IsNavigationOpen);
                viewModel.IsNavigationOpen = !viewModel.IsNavigationOpen;
                Log.Information("Navigation toggled - New state: {IsOpen}", viewModel.IsNavigationOpen);
            }
            else
            {
                Log.Warning("OnToggleNavigationClick - Invalid DataContext: {DataContextType}", DataContext?.GetType().Name);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling toggle navigation click - Sender: {SenderType}", sender?.GetType().Name);
        }
    }

    /// <summary>
    /// Handles the maximize clicked event from FilePreviewControl to scroll to the assignment header
    /// </summary>
    private void OnFilePreviewMaximizeClicked(object sender, RoutedEventArgs e)
    {
        Log.Information("OnFilePreviewMaximizeClicked called. Sender: {SenderType}", sender?.GetType().Name);
        
        // Check if this is our custom event args with the FilePreviewControl
        if (e is FilePreviewControl.MaximizeClickedEventArgs maximizeArgs)
        {
            Log.Information("Received MaximizeClickedEventArgs with FilePreviewControl");
            ScrollToAssignmentHeader(maximizeArgs.FilePreviewControl);
        }
        else
        {
            Log.Warning("Received unexpected event args type: {ArgsType}", e.GetType().Name);
        }
    }


    /// <summary>
    /// Scrolls to the assignment header that contains the given FilePreviewControl
    /// </summary>
    private void ScrollToAssignmentHeader(FilePreviewControl filePreviewControl)
    {
        Log.Information("ScrollToAssignmentHeader called for FilePreviewControl");
        
        var mainScrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
        if (mainScrollViewer == null) 
        {
            Log.Warning("MainScrollViewer not found, cannot scroll");
            return;
        }
        
        // Try simple BringIntoView first as a fallback
        try
        {
            var assignmentHeaderBorder = FindAssignmentHeaderBorder(filePreviewControl);
            if (assignmentHeaderBorder != null)
            {
                Log.Information("Trying simple BringIntoView as fallback");
                
                // Use BringIntoView with specific parameters to ensure it scrolls to the top
                assignmentHeaderBorder.BringIntoView(new Rect(0, 0, 100, 100));
                
                // Also try the complex scroll method after a short delay
                _ = Task.Delay(100).ContinueWith(_ => 
                {
                    Dispatcher.UIThread.InvokeAsync(() => 
                    {
                        PerformScroll(filePreviewControl, mainScrollViewer);
                    });
                });
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Simple BringIntoView failed, trying complex scroll method");
        }
        
        // Wait for layout to update before scrolling
        EventHandler? layoutHandler = null;
        var startTime = DateTime.Now;
        
        layoutHandler = (s, e) =>
        {
            // Timeout protection - increased timeout for folder clicks
            if ((DateTime.Now - startTime).TotalMilliseconds > 2000)
            {
                mainScrollViewer.LayoutUpdated -= layoutHandler;
                Log.Warning("Layout update timeout, aborting scroll");
                return;
            }
            
            // Unsubscribe immediately to run only once
            mainScrollViewer.LayoutUpdated -= layoutHandler;
            
            // Now perform the scroll with updated layout
            PerformScroll(filePreviewControl, mainScrollViewer);
        };
        
        mainScrollViewer.LayoutUpdated += layoutHandler;
    }

    /// <summary>
    /// Performs the actual scroll calculation and execution after layout has updated
    /// </summary>
    private void PerformScroll(FilePreviewControl filePreviewControl, ScrollViewer mainScrollViewer)
    {
        Log.Information("PerformScroll called - layout has been updated");
        
        try
        {
            // Find the assignment header border by traversing up the visual tree
            Log.Information("Looking for AssignmentHeaderBorder in visual tree");
            var assignmentHeaderBorder = FindAssignmentHeaderBorder(filePreviewControl);
            Log.Information("AssignmentHeaderBorder found: {HasHeaderBorder}", assignmentHeaderBorder != null);
            
            if (assignmentHeaderBorder == null) 
            {
                Log.Warning("AssignmentHeaderBorder not found, cannot scroll");
                return;
            }

            // Find the scrollable content container (ItemsControl) inside the ScrollViewer
            var itemsControl = mainScrollViewer.GetVisualDescendants().OfType<ItemsControl>().FirstOrDefault();
            if (itemsControl == null)
            {
                Log.Warning("Could not find ItemsControl inside MainScrollViewer");
                return;
            }
            
            Log.Information("Found ItemsControl, calculating absolute position of AssignmentHeaderBorder");
            
            // Calculate the absolute position of the Assignment Header relative to the ItemsControl
            // This gives us the true position in the scrollable content, independent of current scroll
            var absolutePoint = assignmentHeaderBorder.TranslatePoint(new Point(0, 0), itemsControl);
            Log.Information("Absolute point calculated: {HasPoint}, X: {X}, Y: {Y}", 
                absolutePoint.HasValue, absolutePoint?.X, absolutePoint?.Y);
            
            if (absolutePoint.HasValue)
            {
                // The absolute Y position is exactly where we want to scroll to
                var targetY = absolutePoint.Value.Y;
                Log.Information("Target scroll Y position: {TargetY}", targetY);
                
                // Also calculate position relative to ScrollViewer for verification
                var scrollViewerPoint = assignmentHeaderBorder.TranslatePoint(new Point(0, 0), mainScrollViewer);
                Log.Information("Header position relative to ScrollViewer before scroll: {HasPoint}, Y: {Y}", 
                    scrollViewerPoint.HasValue, scrollViewerPoint?.Y);
                
                // Check ScrollViewer extent to ensure we don't scroll beyond available content
                var extent = mainScrollViewer.Extent;
                var viewport = mainScrollViewer.Viewport;
                var maxScrollY = Math.Max(0, extent.Height - viewport.Height);
                
                Log.Information("ScrollViewer extent: {ExtentWidth}x{ExtentHeight}, viewport: {ViewportWidth}x{ViewportHeight}, maxScrollY: {MaxScrollY}", 
                    extent.Width, extent.Height, viewport.Width, viewport.Height, maxScrollY);
                
                // Calculate the optimal scroll position to show the header at the top
                // If the target position is beyond what we can scroll to, scroll to the maximum
                // This will show as much of the assignment as possible
                var optimalY = Math.Min(targetY, maxScrollY);
                Log.Information("Optimal scroll Y: {OptimalY} (target: {TargetY}, max: {MaxY})", optimalY, targetY, maxScrollY);
                
                // If the target is beyond scrollable area, try to scroll to show as much as possible
                if (targetY > maxScrollY)
                {
                    Log.Information("Target Y {TargetY} is beyond max scroll {MaxY}, scrolling to maximum", targetY, maxScrollY);
                    optimalY = maxScrollY;
                }
                
                // Get current offset for X coordinate (keep horizontal scroll unchanged)
                var currentOffset = mainScrollViewer.Offset;
                
                // Scroll to position the assignment header at the top of the viewport
                Log.Information("Setting scroll offset to: X={TargetX}, Y={TargetY}", currentOffset.X, optimalY);
                mainScrollViewer.Offset = new Vector(currentOffset.X, optimalY);
                
                // Verify the scroll was applied
                var actualOffset = mainScrollViewer.Offset;
                Log.Information("Scroll completed. Actual offset: X={ActualX}, Y={ActualY}", 
                    actualOffset.X, actualOffset.Y);
                
                // Additional verification: check if the header is actually at the top
                var headerPositionAfterScroll = assignmentHeaderBorder.TranslatePoint(new Point(0, 0), mainScrollViewer);
                Log.Information("Header position after scroll relative to ScrollViewer: {HasPoint}, Y: {Y}", 
                    headerPositionAfterScroll.HasValue, headerPositionAfterScroll?.Y);
                
                // If the header is still not visible or not at the top, try BringIntoView as a final attempt
                if (headerPositionAfterScroll.HasValue && headerPositionAfterScroll.Value.Y > 50)
                {
                    Log.Information("Header still not at top (Y: {Y}), trying BringIntoView as final attempt", headerPositionAfterScroll.Value.Y);
                    _ = Task.Delay(50).ContinueWith(_ => 
                    {
                        Dispatcher.UIThread.InvokeAsync(() => 
                        {
                            assignmentHeaderBorder.BringIntoView(new Rect(0, 0, 100, 100));
                        });
                    });
                }
            }
            else
            {
                Log.Warning("Could not calculate absolute point for AssignmentHeaderBorder");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error performing scroll to assignment header");
        }
    }

    /// <summary>
    /// Finds the assignment header border by traversing up the visual tree from the FilePreviewControl
    /// The AssignmentHeaderBorder is a sibling of the FilePreviewControl's parent ItemsControl
    /// </summary>
    private Border? FindAssignmentHeaderBorder(FilePreviewControl filePreviewControl)
    {
        Log.Information("FindAssignmentHeaderBorder called");
        
        try
        {
            // Get all visual ancestors of the FilePreviewControl
            var ancestors = filePreviewControl.GetVisualAncestors().ToList();
            Log.Information("Found {AncestorCount} visual ancestors", ancestors.Count);
            
            // The AssignmentHeaderBorder is a sibling in the Grid that contains both the header and the files
            // We need to find the Grid ancestor (should be around index 5 based on logs)
            // Then search its children for the Border with name "AssignmentHeaderBorder"
            
            foreach (var ancestor in ancestors)
            {
                if (ancestor is Grid grid)
                {
                    Log.Information("Found Grid ancestor, checking if it contains our FilePreviewControl");
                    
                    // Check if this Grid contains our specific FilePreviewControl
                    bool containsOurControl = grid.GetVisualDescendants().Contains(filePreviewControl);
                    Log.Information("Grid contains our FilePreviewControl: {Contains}", containsOurControl);
                    
                    if (containsOurControl)
                    {
                        // This is the Grid that contains our FilePreviewControl, look for its AssignmentHeaderBorder
                        var children = grid.GetVisualChildren().ToList();
                        Log.Information("Grid has {ChildCount} children", children.Count);
                        
                        foreach (var child in children)
                        {
                            if (child is Border border && border.Name == "AssignmentHeaderBorder")
                            {
                                Log.Information("Found AssignmentHeaderBorder for this specific FilePreviewControl!");
                                return border;
                            }
                        }
                    }
                }
            }
            
            Log.Warning("AssignmentHeaderBorder not found in visual tree");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error finding assignment header border");
            return null;
        }
    }

    private bool _isProcessingRatingChange = false;

    /// <summary>
    /// Handles the rating changed event from the StarRating control
    /// </summary>
    private async void OnAssignmentRatingChanged(object? sender, int newRating)
    {
        // Prevent re-entrancy
        if (_isProcessingRatingChange)
        {
            Log.Debug("OnAssignmentRatingChanged - Already processing a rating change, skipping");
            return;
        }

        _isProcessingRatingChange = true;
        try
        {
            Log.Information("OnAssignmentRatingChanged called - Sender: {SenderType}, NewRating: {NewRating}",
                sender?.GetType().Name, newRating);

            if (sender is not Components.StarRating starRating)
            {
                Log.Warning("OnAssignmentRatingChanged - sender is not a StarRating control");
                return;
            }

            // Get the assignment name from the DataContext
            if (starRating.DataContext is not AssignmentGroup assignmentGroup)
            {
                Log.Warning("OnAssignmentRatingChanged - DataContext is not an AssignmentGroup");
                return;
            }

            if (string.IsNullOrWhiteSpace(assignmentGroup.AssignmentName))
            {
                Log.Warning("OnAssignmentRatingChanged - Assignment name is null or empty");
                return;
            }

            if (DataContext is not StudentDetailViewModel viewModel)
            {
                Log.Warning("OnAssignmentRatingChanged - DataContext is not a StudentDetailViewModel");
                return;
            }

            if (viewModel.Student == null)
            {
                Log.Warning("OnAssignmentRatingChanged - Student is null");
                return;
            }

            Log.Information("Rating changed for assignment {AssignmentName} to {Rating}, Student: {StudentName}",
                assignmentGroup.AssignmentName, newRating, viewModel.Student.Name);

            // Don't manually set assignmentGroup.Rating here - the TwoWay binding handles it automatically
            // Setting it manually causes a feedback loop with the binding system
            
            // Save the rating to database
            await viewModel.SaveAssignmentRatingAsync(assignmentGroup.AssignmentName, newRating);

            Log.Information("Rating saved successfully for assignment {AssignmentName}", assignmentGroup.AssignmentName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling assignment rating change - {ExceptionType}: {Message}\nStack Trace: {StackTrace}", 
                ex.GetType().Name, ex.Message, ex.StackTrace);
        }
        finally
        {
            _isProcessingRatingChange = false;
        }
    }

    /// <summary>
    /// Handles the toggle notes button click
    /// </summary>
    private void OnToggleNotesClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Prevent event from bubbling to header click handler
            e.Handled = true;

            if (sender is not Button button)
            {
                Log.Warning("OnToggleNotesClick - sender is not a Button");
                return;
            }

            // Get the assignment from the DataContext
            if (button.DataContext is not AssignmentGroup assignmentGroup)
            {
                Log.Warning("OnToggleNotesClick - DataContext is not an AssignmentGroup");
                return;
            }

            // Toggle the notes expanded state
            assignmentGroup.IsNotesExpanded = !assignmentGroup.IsNotesExpanded;

            // Also expand the assignment content when opening notes
            if (assignmentGroup.IsNotesExpanded && !assignmentGroup.IsExpanded)
            {
                assignmentGroup.IsExpanded = true;
                
                // Auto-expand all code/text files when assignment expands
                foreach (var file in assignmentGroup.Files)
                {
                    if (file.IsCode || file.IsText)
                    {
                        file.IsExpanded = true;
                    }
                }
                
                Log.Information("Expanded assignment {AssignmentName} and auto-expanded files when opening notes",
                    assignmentGroup.AssignmentName);
            }

            Log.Information("Toggled notes for assignment {AssignmentName} - IsExpanded: {IsExpanded}",
                assignmentGroup.AssignmentName, assignmentGroup.IsNotesExpanded);

            // Find the AssignmentContentGrid - it's a sibling, not an ancestor
            // Navigate up to the parent Border, then down to find the grid
            var parentBorder = button.GetVisualAncestors()
                .OfType<Border>()
                .FirstOrDefault(b => b.Name == "AssignmentHeaderBorder");

            Grid? grid = null;
            if (parentBorder != null)
            {
                // Get the parent of the border (should be the Grid with Row="0" and Row="1")
                var parentGrid = parentBorder.GetVisualAncestors().OfType<Grid>().FirstOrDefault();
                if (parentGrid != null)
                {
                    // Find the AssignmentContentGrid in the parent's children
                    grid = parentGrid.GetVisualDescendants()
                        .OfType<Grid>()
                        .FirstOrDefault(g => g.Name == "AssignmentContentGrid");
                }
            }

            if (grid != null && grid.ColumnDefinitions.Count >= 3)
            {
                var notesColumnDef = grid.ColumnDefinitions[2];

                if (assignmentGroup.IsNotesExpanded)
                {
                    // Opening notes - use saved width, or default to 220 if too small
                    var width = assignmentGroup.NotesSidebarWidth;
                    if (width < 150)
                    {
                        width = 220; // Use default width if saved width is too small
                        assignmentGroup.NotesSidebarWidth = width; // Update the saved width
                    }
                    else
                    {
                        width = Math.Min(600, width); // Clamp to max
                    }

                    notesColumnDef.Width = new GridLength(width, GridUnitType.Pixel);
                    notesColumnDef.MinWidth = 150;
                    notesColumnDef.MaxWidth = 600;
                    
                    // Track that notes were just opened
                    _notesJustOpened[assignmentGroup.AssignmentName] = true;
                    
                    // Focus the notes TextBox when opening
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            // Find the TextBox in the notes sidebar (Column 2 of the grid)
                            var notesTextBox = grid.GetVisualDescendants()
                                .OfType<TextBox>()
                                .FirstOrDefault();
                            
                            if (notesTextBox != null)
                            {
                                notesTextBox.Focus();
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Could not focus notes TextBox");
                        }
                    }, DispatcherPriority.Loaded);
                    
                    Log.Information("Opened notes sidebar for {Assignment} - restored width to {Width}",
                        assignmentGroup.AssignmentName, width);
                }
                else
                {
                    // Closing notes - collapse to 0 and remove constraints
                    notesColumnDef.Width = new GridLength(0, GridUnitType.Pixel);
                    notesColumnDef.MinWidth = 0;
                    notesColumnDef.MaxWidth = double.PositiveInfinity;
                    
                    // Clear the just opened flag
                    _notesJustOpened.Remove(assignmentGroup.AssignmentName);
                    
                    Log.Information("Closed notes sidebar for {Assignment} - collapsed to 0",
                        assignmentGroup.AssignmentName);
                }
            }
            else
            {
                Log.Warning("Could not find AssignmentContentGrid to update column width");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error toggling notes");
        }
    }

    /// <summary>
    /// Handles GotFocus event for notes TextBox to add date heading if needed
    /// </summary>
    private void OnNotesTextBoxGotFocus(object? sender, GotFocusEventArgs e)
    {
        try
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            // Get the assignment from the DataContext
            if (textBox.DataContext is not AssignmentGroup assignmentGroup)
            {
                return;
            }

            var assignmentName = assignmentGroup.AssignmentName;
            var notes = assignmentGroup.Notes ?? string.Empty;

            // If notes are empty, add a date heading when user clicks to focus
            if (string.IsNullOrWhiteSpace(notes))
            {
                var dateHeading = GetDateHeading(DateTime.Now);
                assignmentGroup.Notes = dateHeading;
                
                // Move cursor to after the date heading
                Dispatcher.UIThread.Post(() =>
                {
                    textBox.CaretIndex = textBox.Text?.Length ?? 0;
                }, DispatcherPriority.Normal);
            }
            else
            {
                // Check if notes were just opened and we need to add a new date heading
                if (_notesJustOpened.TryGetValue(assignmentName, out var justOpened) && justOpened)
                {
                    var todayHeading = GetDateHeading(DateTime.Now).Trim();
                    var lastHeading = GetLastDateHeading(notes);

                    // If last heading is not today's date, add a new date heading and move the caret under it
                    if (lastHeading == null || !string.Equals(lastHeading.Trim(), todayHeading, StringComparison.OrdinalIgnoreCase))
                    {
                        // Avoid duplicate if text already ends with today's heading
                        var endsWithToday = notes.TrimEnd().EndsWith(todayHeading, StringComparison.OrdinalIgnoreCase);
                        if (!endsWithToday)
                        {
                            var newHeading = "\n\n" + todayHeading; // todayHeading already includes trailing \n\n
                            assignmentGroup.Notes = notes + newHeading;

                            // Move cursor to after the new date heading
                            Dispatcher.UIThread.Post(() =>
                            {
                                textBox.CaretIndex = assignmentGroup.Notes.Length;
                            }, DispatcherPriority.Normal);
                        }
                        else
                        {
                            // If it already ends with today's heading, just place caret at the end
                            Dispatcher.UIThread.Post(() =>
                            {
                                textBox.CaretIndex = notes.Length;
                            }, DispatcherPriority.Normal);
                        }
                    }

                    // Clear the flag
                    _notesJustOpened[assignmentName] = false;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling notes TextBox focus");
        }
    }

    /// <summary>
    /// Gets a formatted date heading (without ##, with capitalized month)
    /// </summary>
    private string GetDateHeading(DateTime date)
    {
        // Format: "October 31, 2025" - MMMM already capitalizes the month
        return $"{date:MMMM d, yyyy}\n\n";
    }

    /// <summary>
    /// Gets the last date heading from notes text
    /// </summary>
    private string? GetLastDateHeading(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return null;

        // Look for the last date heading pattern (Month Day, Year)
        // Pattern: "MonthName Day, Year" followed by optional whitespace and newlines
        var lines = notes.Split('\n');
        foreach (var line in lines.Reverse())
        {
            var trimmed = line.Trim();
            // Check if line matches date pattern: "MonthName Day, Year" (no ##)
            // Example: "October 31, 2025"
            if (!string.IsNullOrWhiteSpace(trimmed) && 
                trimmed.Contains(',') && 
                !trimmed.StartsWith("##"))
            {
                // Check if it looks like a date (has month name pattern and comma)
                // This is a simple check - month names are typically longer than 3 chars
                var parts = trimmed.Split(',');
                if (parts.Length == 2 && parts[0].Trim().Split(' ').Length >= 2)
                {
                    return trimmed;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Removes date headings that don't have content below them
    /// </summary>
    private string CleanEmptyDateHeadings(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return notes;

        // Split by date headings (pattern: "Month Day, Year" followed by newlines)
        var lines = notes.Split('\n').ToList();
        var cleaned = new List<string>();
        
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            
            // Check if this line is a date heading (Month Day, Year pattern)
            bool isDateHeading = !string.IsNullOrWhiteSpace(trimmed) && 
                                 trimmed.Contains(',') && 
                                 !trimmed.StartsWith("##") &&
                                 trimmed.Split(',').Length == 2 &&
                                 trimmed.Split(',')[0].Trim().Split(' ').Length >= 2;
            
            if (isDateHeading)
            {
                // Look ahead to see if there's actual content after this date heading
                bool hasContent = false;
                bool isLastHeading = true; // Check if this is the last date heading
                
                for (int j = i + 1; j < lines.Count; j++)
                {
                    var nextLine = lines[j].Trim();
                    // Skip empty lines and newlines
                    if (string.IsNullOrWhiteSpace(nextLine))
                        continue;
                    
                    // Check if next non-empty line is another date heading
                    bool nextIsDateHeading = nextLine.Contains(',') && 
                                            !nextLine.StartsWith("##") &&
                                            nextLine.Split(',').Length == 2 &&
                                            nextLine.Split(',')[0].Trim().Split(' ').Length >= 2;
                    
                    if (nextIsDateHeading)
                    {
                        isLastHeading = false;
                        // Found another date heading without content in between
                        // This means the current date heading has no content
                        break;
                    }
                    else
                    {
                        hasContent = true;
                        break;
                    }
                }
                
                // Keep the date heading if:
                // 1. It has content below it, OR
                // 2. It's the last heading in the notes (user might be typing there)
                if (hasContent || isLastHeading)
                {
                    cleaned.Add(line);
                }
                // Otherwise skip this date heading (it has no content and isn't the last one)
            }
            else
            {
                // Not a date heading, keep the line
                cleaned.Add(line);
            }
        }
        
        var result = string.Join("\n", cleaned);
        
        // Final cleanup: if the result ends with only a date heading and whitespace (no content after it), remove it
        // This handles the case where user added a date heading but never typed anything
        var trimmedResult = result.TrimEnd();
        if (!string.IsNullOrWhiteSpace(trimmedResult))
        {
            var resultLines = trimmedResult.Split('\n');
            if (resultLines.Length > 0)
            {
                var lastLine = resultLines.Last().Trim();
                // Check if last non-empty line is a date heading
                bool lastLineIsDateHeading = !string.IsNullOrWhiteSpace(lastLine) && 
                                            lastLine.Contains(',') && 
                                            !lastLine.StartsWith("##") &&
                                            lastLine.Split(',').Length == 2 &&
                                            lastLine.Split(',')[0].Trim().Split(' ').Length >= 2;
                
                if (lastLineIsDateHeading)
                {
                    // Check if this is truly the last thing (no content after it)
                    // Look backwards from the last line to see if there's any non-empty, non-date-heading content
                    bool hasContentAfterLastHeading = false;
                    for (int i = resultLines.Length - 2; i >= 0; i--)
                    {
                        var line = resultLines[i].Trim();
                        if (string.IsNullOrWhiteSpace(line))
                            continue;
                        
                        // Check if this line is a date heading
                        bool isDateHeading = line.Contains(',') && 
                                           !line.StartsWith("##") &&
                                           line.Split(',').Length == 2 &&
                                           line.Split(',')[0].Trim().Split(' ').Length >= 2;
                        
                        if (!isDateHeading)
                        {
                            // Found non-date-heading content after the last date heading
                            hasContentAfterLastHeading = true;
                            break;
                        }
                    }
                    
                    // Only remove the last date heading if there's NO content after it
                    if (!hasContentAfterLastHeading)
                    {
                        // Find the position of this last date heading and remove everything from it to the end
                        var lastHeadingIndex = trimmedResult.LastIndexOf(lastLine);
                        if (lastHeadingIndex > 0)
                        {
                            // Check if there's content before this last heading
                            var beforeHeading = trimmedResult.Substring(0, lastHeadingIndex).TrimEnd();
                            // Only remove if there's actual content before it (not just whitespace)
                            if (!string.IsNullOrWhiteSpace(beforeHeading))
                            {
                                return beforeHeading;
                            }
                        }
                    }
                }
            }
        }
        
        return result;
    }

    /// <summary>
    /// Handles text changed event for notes TextBox with auto-save debouncing
    /// </summary>
    private void OnNotesTextChanged(object? sender, TextChangedEventArgs e)
    {
        try
        {
            if (sender is not TextBox textBox)
            {
                Log.Warning("OnNotesTextChanged - sender is not a TextBox");
                return;
            }

            // Get the assignment from the DataContext
            if (textBox.DataContext is not AssignmentGroup assignmentGroup)
            {
                Log.Warning("OnNotesTextChanged - DataContext is not an AssignmentGroup");
                return;
            }

            if (DataContext is not StudentDetailViewModel viewModel)
            {
                Log.Warning("OnNotesTextChanged - DataContext is not a StudentDetailViewModel");
                return;
            }

            var assignmentName = assignmentGroup.AssignmentName;
            var notes = assignmentGroup.Notes ?? string.Empty;

            // Check if user is typing at the end and needs a new date heading
            // This handles the case where user is continuing to type on a new day
            // Only check on the first character typed (when text length just increased by 1)
            if (!string.IsNullOrWhiteSpace(notes) && textBox.CaretIndex >= notes.Length - 1)
            {
                var todayHeading = GetDateHeading(DateTime.Now).Trim();
                var lastHeading = GetLastDateHeading(notes);
                
                // If last heading exists but is not today's, and cursor is at the end, add new heading
                if (lastHeading != null && !string.Equals(lastHeading.Trim(), todayHeading, StringComparison.OrdinalIgnoreCase))
                {
                    // Check if the text doesn't already end with today's heading (avoid duplicates)
                    var trimmedNotes = notes.TrimEnd();
                    var trimmedTodayHeading = todayHeading.Trim();
                    var todayHeadingFull = GetDateHeading(DateTime.Now).TrimEnd();
                    
                    // Only add if the notes don't already end with today's heading
                    if (!trimmedNotes.EndsWith(trimmedTodayHeading, StringComparison.OrdinalIgnoreCase) &&
                        !trimmedNotes.EndsWith(todayHeadingFull, StringComparison.OrdinalIgnoreCase))
                    {
                        // Add new date heading with line breaks before it
                        var newHeading = $"\n\n{GetDateHeading(DateTime.Now)}";
                        assignmentGroup.Notes = notes + newHeading;
                        
                        // Update cursor position
                        Dispatcher.UIThread.Post(() =>
                        {
                            textBox.CaretIndex = assignmentGroup.Notes.Length;
                        }, DispatcherPriority.Normal);
                        
                        // Return early to avoid triggering auto-save with the incomplete text
                        return;
                    }
                }
            }

            // Cancel existing timer if any
            if (_notesAutoSaveTimers.TryGetValue(assignmentName, out var existingTimer))
            {
                existingTimer.Stop();
                _notesAutoSaveTimers.Remove(assignmentName);
            }

            // Create new timer for auto-save (500ms debounce)
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };

            timer.Tick += async (s, args) =>
            {
                timer.Stop();
                _notesAutoSaveTimers.Remove(assignmentName);

                try
                {
                    Log.Information("Auto-saving notes for assignment {AssignmentName}", assignmentName);
                    
                    // Clean notes: remove date headings that don't have content below them
                    var notesToClean = assignmentGroup.Notes ?? string.Empty;
                    var cleanedNotes = CleanEmptyDateHeadings(notesToClean);
                    
                    // Update the assignment group with cleaned notes so UI reflects the changes
                    if (cleanedNotes != assignmentGroup.Notes)
                    {
                        assignmentGroup.Notes = cleanedNotes;
                    }
                    
                    await viewModel.SaveAssignmentNoteAsync(assignmentName, cleanedNotes);
                    Log.Information("Notes auto-saved successfully for assignment {AssignmentName}", assignmentName);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error auto-saving notes for assignment {AssignmentName}", assignmentName);
                }
            };

            _notesAutoSaveTimers[assignmentName] = timer;
            timer.Start();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling notes text change");
        }
    }

    /// <summary>
    /// Handles the open in browser button click for an assignment
    /// </summary>
    private void OnOpenInBrowserClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Prevent event from bubbling to header click handler
            e.Handled = true;

            if (sender is not Button button)
            {
                Log.Warning("OnOpenInBrowserClick - sender is not a Button");
                return;
            }

            // Get the assignment from the DataContext
            if (button.DataContext is not AssignmentGroup assignmentGroup)
            {
                Log.Warning("OnOpenInBrowserClick - DataContext is not an AssignmentGroup");
                return;
            }

            // Find the first Google Doc file in this assignment
            var googleDocFile = assignmentGroup.Files.FirstOrDefault(f => f.IsGoogleDoc);
            if (googleDocFile?.GoogleDocMetadata != null)
            {
                var url = googleDocFile.GoogleDocUrl;
                if (string.IsNullOrEmpty(url))
                {
                    url = googleDocFile.GoogleDocMetadata.GetEditUrl();
                }

                if (!string.IsNullOrEmpty(url))
                {
                    Log.Information("Opening Google Doc in browser: {Url}", url);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening Google Doc in browser");
        }
    }

}

