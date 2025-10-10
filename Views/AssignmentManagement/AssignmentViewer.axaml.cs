using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SchoolOrganizer.ViewModels;
using SchoolOrganizer.Services;
using SchoolOrganizer.Models;
using SchoolOrganizer.Views.Components;
using Serilog;

namespace SchoolOrganizer.Views.AssignmentManagement;

/// <summary>
/// Window for displaying a student's downloaded assignments
/// </summary>
public partial class AssignmentViewer : Window
{
    private FileViewerScrollService? _scrollService;
    private readonly PanelManagementService _panelManagementService;

    public AssignmentViewer()
    {
        InitializeComponent();
        _panelManagementService = new PanelManagementService();
        InitializeScrollService();
    }

    public AssignmentViewer(StudentDetailViewModel viewModel) : this()
    {
        DataContext = viewModel;
        ConnectScrollServiceToViewModel();
        InitializeToggleButtonState();
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
    
    private void InitializeToggleButtonState()
    {
        // Initialize the separator button state
        var separatorToggleButton = this.FindControl<Button>("SeparatorToggleButton");
        
        if (separatorToggleButton != null)
        {
            // Set initial chevron direction (left-pointing since explorer starts open)
            var textBlock = separatorToggleButton.Content as TextBlock;
            if (textBlock != null)
            {
                textBlock.Text = "‹"; // Left-pointing chevron for collapse
            }
        }
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

    private async void OnTreeViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is StudentDetailViewModel viewModel && 
            sender is TreeView treeView &&
            treeView.SelectedItem is FileTreeNode selectedNode)
        {
            viewModel.SelectedFile = selectedNode;
            
            // Load content when a file is selected
            if (!selectedNode.IsDirectory)
            {
                _ = selectedNode.LoadContentAsync();
            }

            // Scroll to the corresponding assignment group in the Files Viewer
            if (_scrollService != null && selectedNode.IsDirectory)
            {
                await _scrollService.ScrollToAssignmentGroupAsync(selectedNode, viewModel.AllFilesGrouped ?? new ObservableCollection<AssignmentGroup>());
            }
        }
    }


    private void OnToggleExplorerClick(object sender, RoutedEventArgs e)
    {
        var mainGrid = this.FindControl<Grid>("MainGrid");
        var explorerPanel = this.FindControl<Border>("ExplorerPanel");
        var mainContentPanel = this.FindControl<Border>("MainContentPanel");
        var separatorPanel = this.FindControl<Border>("SeparatorPanel");
        var separatorToggleButton = this.FindControl<Button>("SeparatorToggleButton");
        
        if (mainGrid != null && explorerPanel != null && mainContentPanel != null && 
            separatorPanel != null && separatorToggleButton != null)
        {
            _panelManagementService.ToggleExplorerPanel(mainGrid, explorerPanel, mainContentPanel, 
                separatorPanel, separatorToggleButton);
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
        
        // Wait for layout to update before scrolling
        EventHandler? layoutHandler = null;
        var startTime = DateTime.Now;
        
        layoutHandler = (s, e) =>
        {
            // Timeout protection
            if ((DateTime.Now - startTime).TotalMilliseconds > 500)
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
                
                // Check ScrollViewer extent to ensure we don't scroll beyond available content
                var extent = mainScrollViewer.Extent;
                var viewport = mainScrollViewer.Viewport;
                var maxScrollY = Math.Max(0, extent.Height - viewport.Height);
                
                Log.Information("ScrollViewer extent: {ExtentWidth}x{ExtentHeight}, viewport: {ViewportWidth}x{ViewportHeight}, maxScrollY: {MaxScrollY}", 
                    extent.Width, extent.Height, viewport.Width, viewport.Height, maxScrollY);
                
                // Clamp the scroll position to valid range
                var clampedY = Math.Min(Math.Max(0, targetY), maxScrollY);
                Log.Information("Clamped scroll Y from {OriginalY} to {ClampedY}", targetY, clampedY);
                
                // Get current offset for X coordinate (keep horizontal scroll unchanged)
                var currentOffset = mainScrollViewer.Offset;
                
                // Scroll to position the assignment header at the top of the viewport
                Log.Information("Setting scroll offset to: X={TargetX}, Y={TargetY}", currentOffset.X, clampedY);
                mainScrollViewer.Offset = new Vector(currentOffset.X, clampedY);
                
                // Verify the scroll was applied
                var actualOffset = mainScrollViewer.Offset;
                Log.Information("Scroll completed. Actual offset: X={ActualX}, Y={ActualY}", 
                    actualOffset.X, actualOffset.Y);
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
}

