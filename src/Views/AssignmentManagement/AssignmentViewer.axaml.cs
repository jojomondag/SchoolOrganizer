using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SchoolOrganizer.Src.ViewModels;
using SchoolOrganizer.Src.Services;
using SchoolOrganizer.Src.Models.Assignments;
using SchoolOrganizer.Src.Models.UI;
using SchoolOrganizer.Src.Views.Components;
using Serilog;

namespace SchoolOrganizer.Src.Views.AssignmentManagement;

/// <summary>
/// Window for displaying a student's downloaded assignments
/// </summary>
public partial class AssignmentViewer : Window
{
    private FileViewerScrollService? _scrollService;
    private DateTime _lastScrollTime = DateTime.MinValue;

    public AssignmentViewer()
    {
        InitializeComponent();
        InitializeScrollService();
    }

    public AssignmentViewer(StudentDetailViewModel viewModel) : this()
    {
        DataContext = viewModel;
        ConnectScrollServiceToViewModel();
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
    /// Initializes the scroll service after the UI components are loaded
    /// </summary>
    private void InitializeScrollService()
    {
        try
        {
            var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
            var itemsControl = scrollViewer?.FindDescendantOfType<ItemsControl>() ?? 
                              scrollViewer?.GetVisualDescendants().OfType<ItemsControl>().FirstOrDefault();

            if (scrollViewer != null && itemsControl != null)
            {
                _scrollService = new FileViewerScrollService(scrollViewer, itemsControl);
                ConnectScrollServiceToViewModel();
            }
            else
            {
                // Schedule a retry after a short delay to allow UI to fully load
                _ = Task.Delay(500).ContinueWith(_ => 
                {
                    Dispatcher.UIThread.InvokeAsync(InitializeScrollService);
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing FileViewerScrollService");
        }
    }

    /// <summary>
    /// Connects the scroll service to the ViewModel if available
    /// </summary>
    private void ConnectScrollServiceToViewModel()
    {
        if (_scrollService != null && DataContext is StudentDetailViewModel viewModel)
        {
            viewModel.SetScrollService(_scrollService);
        }
    }

    // Removed unused event handlers - no DataGrid in current UI




    /// <summary>
    /// Handles assignment navigation button clicks
    /// </summary>
    private async void OnAssignmentClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("OnAssignmentClick called - Sender: {SenderType}, Tag: {Tag}", 
                sender?.GetType().Name, (sender as Button)?.Tag);
            
            if (sender is Button button && button.Tag is string assignmentName && 
                DataContext is StudentDetailViewModel viewModel)
            {
                Log.Information("Executing assignment navigation for: {AssignmentName}", assignmentName);
                await viewModel.ScrollToAssignmentAsync(assignmentName);
                Log.Information("Assignment navigation completed for: {AssignmentName}", assignmentName);
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
    /// Handles view mode button clicks
    /// </summary>
    private void OnViewModeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("OnViewModeClick called - Sender: {SenderType}, Tag: {Tag}", 
                sender?.GetType().Name, (sender as Button)?.Tag);
            
            if (sender is Button button && button.Tag is string viewMode && 
                DataContext is StudentDetailViewModel viewModel)
            {
                Log.Information("Changing view mode to: {ViewMode}", viewMode);
                viewModel.SelectedViewMode = viewMode;
                Log.Information("View mode changed to: {ViewMode}", viewMode);
            }
            else
            {
                Log.Warning("OnViewModeClick - Invalid sender or DataContext. Sender: {SenderType}, DataContext: {DataContextType}", 
                    sender?.GetType().Name, DataContext?.GetType().Name);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling view mode click - Sender: {SenderType}", sender?.GetType().Name);
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
    /// Gets the assignment name from a FileTreeNode
    /// </summary>
    private string GetAssignmentNameFromNode(FileTreeNode node)
    {
        // If the node has an assignment name, use it
        if (!string.IsNullOrEmpty(node.AssignmentName))
        {
            return node.AssignmentName;
        }

        // If it's a top-level directory, use the folder name as assignment name
        if (node.IsDirectory)
        {
            // Check if this is a top-level assignment folder
            var pathParts = node.RelativePath.Split('/', '\\');
            if (pathParts.Length == 1)
            {
                return node.Name;
            }
            else if (pathParts.Length > 1)
            {
                return pathParts[0]; // Use the root assignment folder name
            }
        }

        // Fallback: try to extract from relative path
        if (!string.IsNullOrEmpty(node.RelativePath))
        {
            var pathParts = node.RelativePath.Split('/', '\\');
            if (pathParts.Length > 0 && !string.IsNullOrEmpty(pathParts[0]))
            {
                return pathParts[0];
            }
        }

        return node.Name;
    }

    /// <summary>
    /// Finds a FilePreviewControl for the given assignment name by searching the visual tree
    /// </summary>
    private FilePreviewControl? FindFilePreviewControlForAssignment(string assignmentName)
    {
        Log.Information("FindFilePreviewControlForAssignment called for assignment: {AssignmentName}", assignmentName);
        
        try
        {
            var mainContentPanel = this.FindControl<Border>("MainContentPanel");
            if (mainContentPanel == null)
            {
                Log.Warning("MainContentPanel not found");
                return null;
            }

            // Get all FilePreviewControl instances in the main content panel
            var filePreviewControls = mainContentPanel.GetVisualDescendants()
                .OfType<FilePreviewControl>()
                .ToList();

            Log.Information("Found {ControlCount} FilePreviewControl instances", filePreviewControls.Count);

            // Find the first FilePreviewControl whose DataContext is a StudentFile with matching assignment name
            foreach (var control in filePreviewControls)
            {
                if (control.DataContext is StudentFile studentFile)
                {
                    Log.Information("Found FilePreviewControl with StudentFile: {FileName}, Assignment: {AssignmentName}", 
                        studentFile.FileName, studentFile.AssignmentName);
                    
                    if (string.Equals(studentFile.AssignmentName, assignmentName, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Information("Found matching FilePreviewControl for assignment: {AssignmentName}", assignmentName);
                        return control;
                    }
                }
            }

            Log.Warning("No FilePreviewControl found for assignment: {AssignmentName}", assignmentName);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error finding FilePreviewControl for assignment: {AssignmentName}", assignmentName);
            return null;
        }
    }
}

