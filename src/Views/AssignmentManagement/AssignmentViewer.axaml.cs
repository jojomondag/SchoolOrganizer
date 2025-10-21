using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
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
/// Window for displaying a student's downloaded assignments
/// </summary>
public partial class AssignmentViewer : Window
{
    private Dictionary<string, DispatcherTimer> _notesAutoSaveTimers = new();
    private Dictionary<string, DispatcherTimer> _widthAutoSaveTimers = new();

    public AssignmentViewer()
    {
        InitializeComponent();
    }

    public AssignmentViewer(StudentDetailViewModel viewModel) : this()
    {
        DataContext = viewModel;
        SubscribeToFilePreviewEvents();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
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
    /// Handles assignment navigation button clicks
    /// </summary>
    private void OnAssignmentClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("OnAssignmentClick called - Sender: {SenderType}, Tag: {Tag}",
                sender?.GetType().Name, (sender as Button)?.Tag);

            if (sender is Button button && button.Tag is string assignmentName)
            {
                Log.Information("Scrolling to assignment: {AssignmentName}", assignmentName);

                // Find the assignment header with this name and scroll to it
                var mainScrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
                if (mainScrollViewer != null)
                {
                    // Find all assignment headers
                    var headers = mainScrollViewer.GetVisualDescendants()
                        .OfType<Border>()
                        .Where(b => b.Name == "AssignmentHeaderBorder")
                        .ToList();

                    // Find the one with matching assignment name in its DataContext
                    var targetHeader = headers.FirstOrDefault(h =>
                        h.DataContext is AssignmentGroup ag && ag.AssignmentName == assignmentName);

                    if (targetHeader != null)
                    {
                        targetHeader.BringIntoView(new Rect(0, 0, 100, 100));
                        Log.Information("Scrolled to assignment: {AssignmentName}", assignmentName);
                    }
                }
            }
            else
            {
                Log.Warning("OnAssignmentClick - Invalid sender or DataContext. Sender: {SenderType}, DataContext: {DataContextType}",
                    sender?.GetType().Name, DataContext?.GetType().Name);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling assignment click - Sender: {SenderType}", sender?.GetType().Name);
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

    /// <summary>
    /// Handles the rating changed event from the StarRating control
    /// </summary>
    private async void OnAssignmentRatingChanged(object? sender, int newRating)
    {
        try
        {
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

            if (DataContext is not StudentDetailViewModel viewModel)
            {
                Log.Warning("OnAssignmentRatingChanged - DataContext is not a StudentDetailViewModel");
                return;
            }

            Log.Information("Rating changed for assignment {AssignmentName} to {Rating}",
                assignmentGroup.AssignmentName, newRating);

            // Save the rating
            await viewModel.SaveAssignmentRatingAsync(assignmentGroup.AssignmentName, newRating);

            Log.Information("Rating saved successfully for assignment {AssignmentName}", assignmentGroup.AssignmentName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling assignment rating change");
        }
    }

    /// <summary>
    /// Handles the toggle notes button click
    /// </summary>
    private void OnToggleNotesClick(object? sender, RoutedEventArgs e)
    {
        try
        {
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
                    Log.Information("Opened notes sidebar for {Assignment} - restored width to {Width}",
                        assignmentGroup.AssignmentName, width);
                }
                else
                {
                    // Closing notes - collapse to 0 and remove constraints
                    notesColumnDef.Width = new GridLength(0, GridUnitType.Pixel);
                    notesColumnDef.MinWidth = 0;
                    notesColumnDef.MaxWidth = double.PositiveInfinity;
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
                    await viewModel.SaveAssignmentNoteAsync(assignmentName, assignmentGroup.Notes);
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

