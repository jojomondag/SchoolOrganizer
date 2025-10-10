using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media;
using SchoolOrganizer.ViewModels;
using SchoolOrganizer.Services;
using SchoolOrganizer.Models;
using Serilog;

namespace SchoolOrganizer.Views.ContentView;

/// <summary>
/// Window for displaying a student's downloaded assignments
/// </summary>
public partial class StudentDetailView : Window
{
    private FileViewerScrollService? _scrollService;
    private bool _isExplorerCollapsed = false;
    private bool _isContentMaximized = false;
    
    // Scroll tracking for maximize functionality
    private readonly Dictionary<Button, double> _preMaximizeScrollPositions = new();
    private DispatcherTimer? _scrollAnimationTimer;

    public StudentDetailView()
    {
        InitializeComponent();
        InitializeScrollService();
    }

    public StudentDetailView(StudentDetailViewModel viewModel) : this()
    {
        Log.Information("StudentDetailView constructor called with ViewModel");
        DataContext = viewModel;
        
        // Connect the scroll service to the ViewModel if it's already initialized
        if (_scrollService != null)
        {
            viewModel.SetScrollService(_scrollService);
            Log.Information("Scroll service connected to ViewModel in constructor");
        }
        else
        {
            Log.Information("Scroll service not yet initialized in constructor");
        }
        
        // Initialize the toggle button visibility based on initial state
        InitializeToggleButtonState();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
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
            Log.Information("Starting InitializeScrollService...");
            
            // Find the scroll viewer and items control from the XAML
            var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
            Log.Information("ScrollViewer found: {Found}", scrollViewer != null);
            
            // Try multiple ways to find the ItemsControl
            ItemsControl? itemsControl = null;
            
            if (scrollViewer != null)
            {
                // Method 1: Direct descendant search
                itemsControl = scrollViewer.FindDescendantOfType<ItemsControl>();
                Log.Information("Method 1 - ItemsControl found via FindDescendantOfType: {Found}", itemsControl != null);
                
                if (itemsControl == null)
                {
                    // Method 2: Search in the entire visual tree under ScrollViewer
                    var allChildren = scrollViewer.GetVisualDescendants().OfType<ItemsControl>().ToList();
                    itemsControl = allChildren.FirstOrDefault();
                    Log.Information("Method 2 - ItemsControl found via GetVisualDescendants: {Found}, Total found: {Count}", 
                        itemsControl != null, allChildren.Count);
                }
                
                if (itemsControl == null)
                {
                    // Method 3: Try to find by looking at Content
                    if (scrollViewer.Content is ItemsControl directItemsControl)
                    {
                        itemsControl = directItemsControl;
                        Log.Information("Method 3 - ItemsControl found as direct content: {Found}", itemsControl != null);
                    }
                }
                
                if (itemsControl == null)
                {
                    // Method 4: Search in StackPanel content
                    if (scrollViewer.Content is StackPanel stackPanel)
                    {
                        itemsControl = stackPanel.Children.OfType<ItemsControl>().FirstOrDefault();
                        Log.Information("Method 4 - ItemsControl found in StackPanel: {Found}", itemsControl != null);
                    }
                }
                
                if (itemsControl == null)
                {
                    // Method 5: Deep search through all descendants
                    var allDescendants = scrollViewer.GetVisualDescendants().ToList();
                    Log.Information("Method 5 - Searching through {Count} descendants", allDescendants.Count);
                    foreach (var descendant in allDescendants)
                    {
                        if (descendant is ItemsControl ic)
                        {
                            itemsControl = ic;
                            Log.Information("Method 5 - Found ItemsControl: {Type}", ic.GetType().Name);
                            break;
                        }
                    }
                }
            }

            if (scrollViewer != null && itemsControl != null)
            {
                _scrollService = new FileViewerScrollService(scrollViewer, itemsControl);
                Log.Information("FileViewerScrollService created successfully with ItemsControl type: {ItemsControlType}", 
                    itemsControl.GetType().Name);
                
                // Connect the scroll service to the ViewModel if available
                if (DataContext is StudentDetailViewModel viewModel)
                {
                    viewModel.SetScrollService(_scrollService);
                    Log.Information("Scroll service connected to ViewModel");
                }
                else
                {
                    Log.Warning("DataContext is not StudentDetailViewModel, cannot connect scroll service yet");
                }
                
                Log.Information("FileViewerScrollService initialized successfully");
            }
            else
            {
                Log.Information("Could not find ScrollViewer or ItemsControl for scroll service initialization. ScrollViewer: {SV}, ItemsControl: {IC}", 
                    scrollViewer != null, itemsControl != null);
                
                // Schedule a retry after a short delay to allow UI to fully load
                Log.Information("Scheduling scroll service initialization retry in 500ms...");
                _ = Task.Delay(500).ContinueWith(_ => 
                {
                    Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Log.Information("Retrying scroll service initialization...");
                        InitializeScrollServiceRetry();
                    });
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing FileViewerScrollService");
        }
    }

    /// <summary>
    /// Retry initialization after UI is fully loaded
    /// </summary>
    private void InitializeScrollServiceRetry()
    {
        try
        {
            if (_scrollService != null)
            {
                Log.Information("Scroll service already initialized, skipping retry");
                return;
            }

            var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
            ItemsControl? itemsControl = null;
            
            if (scrollViewer != null)
            {
                // Try all methods again
                itemsControl = scrollViewer.FindDescendantOfType<ItemsControl>();
                
                if (itemsControl == null)
                {
                    var allChildren = scrollViewer.GetVisualDescendants().OfType<ItemsControl>().ToList();
                    itemsControl = allChildren.FirstOrDefault();
                    Log.Information("Retry - Found {Count} ItemsControl instances", allChildren.Count);
                }
                
                if (itemsControl == null && scrollViewer.Content is ItemsControl directItemsControl)
                {
                    itemsControl = directItemsControl;
                }
                
                if (itemsControl == null && scrollViewer.Content is StackPanel stackPanel)
                {
                    itemsControl = stackPanel.Children.OfType<ItemsControl>().FirstOrDefault();
                    Log.Information("Retry - ItemsControl found in StackPanel: {Found}", itemsControl != null);
                }
                
                if (itemsControl == null)
                {
                    var allDescendants = scrollViewer.GetVisualDescendants().ToList();
                    Log.Information("Retry - Searching through {Count} descendants", allDescendants.Count);
                    foreach (var descendant in allDescendants)
                    {
                        if (descendant is ItemsControl ic)
                        {
                            itemsControl = ic;
                            Log.Information("Retry - Found ItemsControl: {Type}", ic.GetType().Name);
                            break;
                        }
                    }
                }
            }

            if (scrollViewer != null && itemsControl != null)
            {
                _scrollService = new FileViewerScrollService(scrollViewer, itemsControl);
                Log.Information("FileViewerScrollService created successfully on retry");
                
                if (DataContext is StudentDetailViewModel viewModel)
                {
                    viewModel.SetScrollService(_scrollService);
                    Log.Information("Scroll service connected to ViewModel on retry");
                }
            }
            else
            {
                Log.Warning("Retry failed - still cannot find required controls");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during scroll service retry initialization");
        }
    }

    private void OnDataGridDoubleClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is StudentDetailViewModel viewModel && 
            sender is DataGrid dataGrid && 
            dataGrid.SelectedItem is StudentFile selectedFile)
        {
            viewModel.OpenFileCommand.Execute(selectedFile);
        }
    }

    private void OnDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Handle selection changes if needed
    }

    private async void OnTreeViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Log.Information("OnTreeViewSelectionChanged called");
        Log.Information("Sender type: {SenderType}", sender?.GetType().Name ?? "null");
        Log.Information("DataContext type: {DataContextType}", DataContext?.GetType().Name ?? "null");
        
        if (DataContext is StudentDetailViewModel viewModel && 
            sender is TreeView treeView)
        {
            Log.Information("DataContext and sender are valid types");
            Log.Information("TreeView.SelectedItem type: {SelectedItemType}", treeView.SelectedItem?.GetType().Name ?? "null");
            
            var selectedNode = treeView.SelectedItem as FileTreeNode;
            if (selectedNode != null)
            {
                Log.Information("TreeView selection changed: {NodeName}, IsDirectory: {IsDirectory}, AssignmentName: {AssignmentName}, FullPath: {FullPath}", 
                    selectedNode.Name, selectedNode.IsDirectory, selectedNode.AssignmentName, selectedNode.FullPath);
                
                Log.Information("Setting SelectedFile on ViewModel...");
                viewModel.SelectedFile = selectedNode;
                Log.Information("SelectedFile set successfully");
                
                // Load content when a file is selected
                if (!selectedNode.IsDirectory)
                {
                    Log.Information("Loading content for file: {FileName}", selectedNode.Name);
                    _ = selectedNode.LoadContentAsync();
                }
                else
                {
                    Log.Information("Selected item is a directory: {DirectoryName}", selectedNode.Name);
                }

                // Scroll to the corresponding assignment group in the Files Viewer
                if (_scrollService != null && selectedNode.IsDirectory)
                {
                    Log.Information("Scroll service available. Attempting to scroll to assignment group for folder: {FolderName}", selectedNode.Name);
                    Log.Information("AllFilesGrouped count: {Count}", viewModel.AllFilesGrouped?.Count ?? 0);
                    
                    var scrolled = await _scrollService.ScrollToAssignmentGroupAsync(selectedNode, viewModel.AllFilesGrouped ?? new ObservableCollection<AssignmentGroup>());
                    
                    if (scrolled)
                    {
                        Log.Information("Successfully scrolled to assignment group for folder: {FolderName}", selectedNode.Name);
                    }
                    else
                    {
                        Log.Warning("Failed to scroll to assignment group for folder: {FolderName}", selectedNode.Name);
                    }
                }
                else if (_scrollService == null)
                {
                    Log.Warning("Scroll service is null, cannot scroll");
                }
                else if (!selectedNode.IsDirectory)
                {
                    Log.Information("Selected item is not a directory, no scrolling needed");
                }
            }
            else
            {
                Log.Warning("TreeView selection changed but selectedNode is null. SelectedItem: {SelectedItem}", treeView.SelectedItem);
            }
        }
        else
        {
            Log.Warning("Invalid DataContext or sender. DataContext is StudentDetailViewModel: {IsViewModel}, Sender is TreeView: {IsTreeView}", 
                DataContext is StudentDetailViewModel, sender is TreeView);
        }
    }

    private void OnFileOpenClick(object sender, RoutedEventArgs e)
    {
        Log.Information("=== OnFileOpenClick called ===");
        Log.Information("Sender type: {SenderType}", sender?.GetType().Name ?? "null");
        Log.Information("DataContext type: {DataContextType}", DataContext?.GetType().Name ?? "null");
        
        if (DataContext is StudentDetailViewModel viewModel && sender is Button button)
        {
            Log.Information("Button clicked, Button DataContext type: {ButtonDataType}", button.DataContext?.GetType().Name ?? "null");
            Log.Information("Button DataContext: {ButtonDataContext}", button.DataContext);
            
            // Check if it's a FileTreeNode (from tree view)
            if (button.DataContext is FileTreeNode fileNode)
            {
                Log.Information("FileTreeNode detected: {FileName}, FullPath: {FullPath}", fileNode.Name, fileNode.FullPath);
                // Create a StudentFile from the FileTreeNode and open it
                var studentFile = new StudentFile
                {
                    FileName = fileNode.Name,
                    FilePath = fileNode.FullPath,
                    AssignmentName = fileNode.AssignmentName,
                    FileSize = fileNode.FileSize,
                    LastModified = fileNode.LastModified,
                    RelativePath = fileNode.RelativePath
                };
                
                Log.Information("Created StudentFile: {FileName}, FilePath: {FilePath}", studentFile.FileName, studentFile.FilePath);
                Log.Information("Executing OpenFileCommand...");
                Log.Information("Command CanExecute: {CanExecute}", viewModel.OpenFileCommand.CanExecute);
                viewModel.OpenFileCommand.Execute(studentFile);
                Log.Information("OpenFileCommand executed");
            }
            // Check if it's a StudentFile (from assignment files)
            else if (button.DataContext is StudentFile studentFile)
            {
                Log.Information("StudentFile detected: {FileName}, FilePath: {FilePath}", studentFile.FileName, studentFile.FilePath);
                Log.Information("Executing OpenFileCommand...");
                Log.Information("Command CanExecute: {CanExecute}", viewModel.OpenFileCommand.CanExecute);
                
                // Try direct method call instead of command
                Log.Information("Calling OpenFile directly...");
                try
                {
                    viewModel.OpenFile(studentFile);
                    Log.Information("OpenFile called successfully");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception calling OpenFile directly: {Message}", ex.Message);
                }
                
                viewModel.OpenFileCommand.Execute(studentFile);
                Log.Information("OpenFileCommand executed");
            }
            else
            {
                Log.Warning("Unknown DataContext type: {DataType}", button.DataContext?.GetType().Name ?? "null");
                Log.Warning("Button DataContext: {DataContext}", button.DataContext);
            }
        }
        else
        {
            Log.Warning("Invalid DataContext or sender in OnFileOpenClick. DataContext is StudentDetailViewModel: {IsViewModel}, Sender is Button: {IsButton}", 
                DataContext is StudentDetailViewModel, sender is Button);
        }
        Log.Information("=== OnFileOpenClick completed ===");
    }

    private void OnFileTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not StudentFile file)
            return;

        if (DataContext is StudentDetailViewModel viewModel)
        {
            // Handle file tap if needed
        }
    }

    private void OnToggleExplorerClick(object sender, RoutedEventArgs e)
    {
        _isExplorerCollapsed = !_isExplorerCollapsed;
        
        // Find the main grid and access its column definitions
        var mainGrid = this.FindControl<Grid>("MainGrid");
        var explorerPanel = this.FindControl<Border>("ExplorerPanel");
        var mainContentPanel = this.FindControl<Border>("MainContentPanel");
        var separatorPanel = this.FindControl<Border>("SeparatorPanel");
        var separatorToggleButton = this.FindControl<Button>("SeparatorToggleButton");
        var explorerTransform = explorerPanel?.RenderTransform as TranslateTransform;
        
        if (mainGrid != null && explorerPanel != null && mainContentPanel != null && separatorPanel != null && separatorToggleButton != null)
        {
            var explorerColumn = mainGrid.ColumnDefinitions[0]; // First column
            
            if (_isExplorerCollapsed)
            {
                // Smooth slide out animation
                if (explorerTransform != null)
                {
                    explorerTransform.X = -300;
                }
                
                // Collapse the explorer column completely
                explorerColumn.Width = new GridLength(0);
                
                // Move separator to column 0 and set its width explicitly
                if (separatorPanel != null)
                {
                    Grid.SetColumn(separatorPanel, 0);
                    separatorPanel.Width = 16; // Set explicit width to match button
                    separatorPanel.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                }
                
                // Expand main content to fill space all the way to the separator
                if (mainContentPanel != null)
                {
                    Grid.SetColumnSpan(mainContentPanel, 3); // Span all 3 columns
                    Grid.SetColumn(mainContentPanel, 0); // Start from first column (next to separator)
                }
                
                // Add left margin to respect the separator bar space
                if (mainContentPanel != null)
                {
                    mainContentPanel.Margin = new Avalonia.Thickness(16, 0, 0, 0); // 16px left margin
                }
                
                // Update separator button chevron to point right (to expand)
                var textBlock = separatorToggleButton.Content as TextBlock;
                if (textBlock != null)
                {
                    textBlock.Text = "›"; // Right-pointing chevron
                }
            }
            else
            {
                // Expand the explorer column first
                explorerColumn.Width = new GridLength(300, GridUnitType.Pixel);
                
                // Smooth slide in animation
                if (explorerTransform != null)
                {
                    explorerTransform.X = 0;
                }
                
                // Move separator back to column 1 (between explorer and content)
                if (separatorPanel != null)
                {
                    Grid.SetColumn(separatorPanel, 1);
                    separatorPanel.Width = double.NaN; // Reset to auto width
                    separatorPanel.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch; // Reset alignment
                }
                
                // Reset main content to normal position
                if (mainContentPanel != null)
                {
                    Grid.SetColumnSpan(mainContentPanel, 1); // Span only 1 column
                    Grid.SetColumn(mainContentPanel, 2); // Start from third column
                }
                
                // Reset margin
                if (mainContentPanel != null)
                {
                    mainContentPanel.Margin = new Avalonia.Thickness(0); // Reset margin
                }
                
                // Update separator button chevron to point left (to collapse)
                var textBlock = separatorToggleButton.Content as TextBlock;
                if (textBlock != null)
                {
                    textBlock.Text = "‹"; // Left-pointing chevron
                }
            }
        }
    }

    private void OnOpenFolderButtonPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (sender is Button button && button.Content is Material.Icons.Avalonia.MaterialIcon icon)
        {
            icon.Kind = Material.Icons.MaterialIconKind.FolderOpen;
        }
    }

    private void OnOpenFolderButtonPointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (sender is Button button && button.Content is Material.Icons.Avalonia.MaterialIcon icon)
        {
            icon.Kind = Material.Icons.MaterialIconKind.Folder;
        }
    }

    private async void OnMaximizerClick(object? sender, RoutedEventArgs e)
    {
        Log.Information("=== OnMaximizerClick called ===");
        
        if (sender is Button button)
        {
            // Get the main scroll viewer for scroll position tracking
            var mainScrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
            if (mainScrollViewer == null)
            {
                Log.Warning("MainScrollViewer not found");
                return;
            }

            // Traverse up to find the parent Grid that contains both the button and the ScrollViewer
            Avalonia.Visual? parent = button;
            while (parent is not null && parent is not Grid { RowDefinitions.Count: >= 2 })
            {
                parent = parent.GetVisualParent();
            }
            
            if (parent is Grid parentGrid)
            {
                // Find the ScrollViewer in Row 1 of the parent Grid
                var scrollViewer = parentGrid.Children
                    .OfType<ScrollViewer>()
                    .FirstOrDefault(sv => Grid.GetRow(sv) == 1);
                
                // Find the maximizer icon (child of the button)
                var maximizerIcon = button.Content as Material.Icons.Avalonia.MaterialIcon;
                
                if (scrollViewer != null && maximizerIcon != null)
                {
                    _isContentMaximized = !_isContentMaximized;
                    
                    if (_isContentMaximized)
                    {
                        // Save current scroll position before maximizing
                        var currentScrollOffset = mainScrollViewer.Offset.Y;
                        _preMaximizeScrollPositions[button] = currentScrollOffset;
                        Log.Information("Saved scroll position: {Offset} for button", currentScrollOffset);
                        
                        // Force layout update and wait for it to complete
                        mainScrollViewer.UpdateLayout();
                        await Task.Delay(50); // Allow layout to settle
                        
                        // Find the file header (Border containing the maximize button) to calculate scroll target
                        var fileHeader = FindFileHeaderBorder(button);
                        if (fileHeader != null)
                        {
                            // Calculate target scroll position to move file header to top
                            var targetOffset = await CalculateScrollTargetForMaximize(fileHeader, mainScrollViewer);
                            Log.Information("Calculated target scroll offset: {Offset}", targetOffset);
                            
                            // Smooth scroll to position the file header at the top
                            SmoothScrollToVerticalOffset(targetOffset);
                        }
                        
                        // Maximize: Remove MaxHeight constraint to use full available space
                        scrollViewer.MaxHeight = double.PositiveInfinity;
                        maximizerIcon.Kind = Material.Icons.MaterialIconKind.FullscreenExit;
                        
                        // Also remove MaxHeight from content elements
                        if (scrollViewer.Content is Grid contentGrid)
                        {
                            foreach (var child in contentGrid.Children)
                            {
                                if (child is Image image)
                                {
                                    image.MaxHeight = double.PositiveInfinity;
                                }
                                else if (child is TextBlock textBlock)
                                {
                                    textBlock.MaxHeight = double.PositiveInfinity;
                                }
                                else if (child is Control control && control.GetType().Name == "SyntaxHighlightedCodeViewer")
                                {
                                    control.MaxHeight = double.PositiveInfinity;
                                }
                            }
                        }
                        
                        Log.Information("Content maximized - using full height");
                    }
                    else
                    {
                        // Restore: Set back to normal height
                        scrollViewer.MaxHeight = 400;
                        maximizerIcon.Kind = Material.Icons.MaterialIconKind.Fullscreen;
                        
                        // Restore MaxHeight on content elements
                        if (scrollViewer.Content is Grid contentGrid)
                        {
                            foreach (var child in contentGrid.Children)
                            {
                                if (child is Image image)
                                {
                                    image.MaxHeight = 300;
                                }
                                else if (child is TextBlock textBlock)
                                {
                                    textBlock.MaxHeight = 300;
                                }
                                else if (child is Control control && control.GetType().Name == "SyntaxHighlightedCodeViewer")
                                {
                                    control.MaxHeight = 300;
                                }
                            }
                        }
                        
                        // Restore scroll position to keep header visible at top
                        if (_preMaximizeScrollPositions.TryGetValue(button, out var savedOffset))
                        {
                            Log.Information("Restoring scroll position: {Offset}", savedOffset);
                            SmoothScrollToVerticalOffset(savedOffset);
                            _preMaximizeScrollPositions.Remove(button);
                        }
                        
                        Log.Information("Content restored to normal height");
                    }
                }
                else
                {
                    Log.Warning("Could not find ScrollViewer or MaximizerIcon. ScrollViewer: {Sv}, Icon: {Icon}", 
                        scrollViewer != null, maximizerIcon != null);
                }
            }
            else
            {
                Log.Warning("Could not find parent Grid for maximizer button");
            }
        }
    }

    private void OnMaximizerButtonPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (sender is Button button && button.Content is Material.Icons.Avalonia.MaterialIcon icon)
        {
            // Change icon on hover for better UX
            if (_isContentMaximized)
            {
                icon.Kind = Material.Icons.MaterialIconKind.FullscreenExit;
            }
            else
            {
                icon.Kind = Material.Icons.MaterialIconKind.Fullscreen;
            }
        }
    }

    private void OnMaximizerButtonPointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (sender is Button button && button.Content is Material.Icons.Avalonia.MaterialIcon icon)
        {
            // Restore original icon
            if (_isContentMaximized)
            {
                icon.Kind = Material.Icons.MaterialIconKind.FullscreenExit;
            }
            else
            {
                icon.Kind = Material.Icons.MaterialIconKind.Fullscreen;
            }
        }
    }

    /// <summary>
    /// Cancels any ongoing scroll animation
    /// </summary>
    private void CancelScrollAnimation()
    {
        try
        {
            if (_scrollAnimationTimer != null)
            {
                _scrollAnimationTimer.Stop();
                _scrollAnimationTimer = null;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error canceling scroll animation");
        }
    }

    /// <summary>
    /// Smoothly scrolls the main scroll viewer to the specified vertical offset
    /// </summary>
    /// <param name="targetOffset">The target vertical offset to scroll to</param>
    /// <param name="durationMs">Duration of the scroll animation in milliseconds</param>
    private void SmoothScrollToVerticalOffset(double targetOffset, int durationMs = 300)
    {
        try
        {
            var mainScrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
            if (mainScrollViewer == null)
            {
                Log.Warning("MainScrollViewer not found for smooth scrolling");
                return;
            }

            CancelScrollAnimation();

            var startOffset = mainScrollViewer.Offset.Y;
            var delta = targetOffset - startOffset;
            
            if (Math.Abs(delta) < 0.5 || durationMs <= 0)
            {
                mainScrollViewer.Offset = new Avalonia.Vector(mainScrollViewer.Offset.X, targetOffset);
                return;
            }

            var startTime = DateTime.UtcNow;
            _scrollAnimationTimer = new DispatcherTimer();
            _scrollAnimationTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps
            _scrollAnimationTimer.Tick += (s, e) =>
            {
                try
                {
                    var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                    var t = Math.Min(1.0, elapsed / durationMs);

                    // Cubic ease-out: 1 - (1 - t)^3
                    var eased = 1.0 - Math.Pow(1.0 - t, 3);

                    var newOffset = startOffset + (delta * eased);
                    mainScrollViewer.Offset = new Avalonia.Vector(mainScrollViewer.Offset.X, newOffset);

                    if (t >= 1.0)
                    {
                        CancelScrollAnimation();
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Scroll animation error");
                    CancelScrollAnimation();
                }
            };

            _scrollAnimationTimer.Start();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error starting smooth scroll animation");
        }
    }

    /// <summary>
    /// Finds the assignment group Border that contains the maximize button
    /// </summary>
    /// <param name="maximizeButton">The maximize button</param>
    /// <returns>The assignment group Border, or null if not found</returns>
    private Border? FindFileHeaderBorder(Button maximizeButton)
    {
        try
        {
            // Use a more direct approach: find the ItemsControl that contains all assignment groups,
            // then find which assignment group contains our button
            var mainScrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
            if (mainScrollViewer == null)
            {
                Log.Warning("MainScrollViewer not found");
                return null;
            }

            // Find the ItemsControl that contains assignment groups
            var assignmentItemsControl = mainScrollViewer.GetVisualDescendants()
                .OfType<ItemsControl>()
                .FirstOrDefault(ic => ic.ItemsSource != null);
            
            if (assignmentItemsControl == null)
            {
                Log.Warning("Assignment ItemsControl not found");
                return null;
            }

            Log.Information("Found assignment ItemsControl with {Count} items", assignmentItemsControl.Items.Count);

            // Find which assignment group container contains our button
            var assignmentContainers = assignmentItemsControl.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.Name != "MainContentPanel" && 
                           b.Name != "ExplorerPanel" && 
                           b.Name != "SeparatorPanel")
                .ToList();

            Log.Information("Found {Count} potential assignment containers", assignmentContainers.Count);

            foreach (var container in assignmentContainers)
            {
                // Check if this container contains our maximize button
                var containsButton = container.GetVisualDescendants()
                    .OfType<Button>()
                    .Contains(maximizeButton);

                if (containsButton)
                {
                    // Verify this is an assignment group by checking for assignment header structure
                    var hasAssignmentHeader = container.GetVisualDescendants()
                        .OfType<TextBlock>()
                        .Any(tb => !string.IsNullOrEmpty(tb.Text) && 
                                  tb.FontWeight == FontWeight.Bold && 
                                  tb.FontSize == 18 &&
                                  tb.HorizontalAlignment == Avalonia.Layout.HorizontalAlignment.Center);

                    if (hasAssignmentHeader)
                    {
                        Log.Information("Found assignment group Border containing maximize button");
                        return container;
                    }
                }
            }

            Log.Warning("Could not find assignment group Border containing maximize button");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error finding assignment group Border");
            return null;
        }
    }

    /// <summary>
    /// Calculates the target scroll offset to position the file header at the top
    /// </summary>
    /// <param name="fileHeader">The file header Border</param>
    /// <param name="scrollViewer">The main scroll viewer</param>
    /// <returns>The target scroll offset</returns>
    private async Task<double> CalculateScrollTargetForMaximize(Border fileHeader, ScrollViewer scrollViewer)
    {
        try
        {
            // Log scroll viewer state for debugging
            Log.Information("ScrollViewer state - Extent: {Extent}, Viewport: {Viewport}, Offset: {Offset}", 
                scrollViewer.Extent, scrollViewer.Viewport, scrollViewer.Offset);
            
            // Get the current scroll position
            var currentOffset = scrollViewer.Offset.Y;
            
            // Log file header bounds
            Log.Information("FileHeader bounds: {Bounds}", fileHeader.Bounds);
            
            // Method 1: Try using TranslatePoint (viewport-relative coordinates)
            var headerPosition = fileHeader.TranslatePoint(new Avalonia.Point(0, 0), scrollViewer);
            if (headerPosition.HasValue)
            {
                Log.Information("TranslatePoint result: {Position}", headerPosition.Value);
                
                // TranslatePoint gives viewport-relative coordinates
                // If headerPosition.Y is positive, header is below viewport top
                // If headerPosition.Y is negative, header is above viewport top
                var targetOffset = currentOffset + headerPosition.Value.Y; // No margin for exact top alignment
                
                // Clamp to valid scroll range
                var maxScroll = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
                targetOffset = Math.Max(0, Math.Min(targetOffset, maxScroll));
                
                Log.Information("Method 1 - Header position: {Position}, Current offset: {Current}, Target offset: {Target}", 
                    headerPosition.Value.Y, currentOffset, targetOffset);
                
                return targetOffset;
            }
            
            // Method 2: Fallback using bounds traversal to get absolute position in scroll content
            Log.Information("TranslatePoint failed, trying bounds traversal method");
            
            // Get the absolute position of the file header in the scroll content
            var absolutePosition = CalculateAbsolutePositionInScrollContent(fileHeader, scrollViewer);
            if (absolutePosition.HasValue)
            {
                var targetOffset = absolutePosition.Value; // No margin for exact top alignment
                
                // Clamp to valid scroll range
                var maxScroll = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
                targetOffset = Math.Max(0, Math.Min(targetOffset, maxScroll));
                
                Log.Information("Method 2 - Absolute position: {Position}, Target offset: {Target}", 
                    absolutePosition.Value, targetOffset);
                
                return targetOffset;
            }
            
            // Method 3: Fallback using BringIntoView and measuring
            Log.Information("Bounds traversal failed, trying BringIntoView method");
            
            // Use BringIntoView to get the element to the top, then measure the offset
            fileHeader.BringIntoView();
            await Task.Delay(100); // Wait for BringIntoView to complete
            
            var newOffset = scrollViewer.Offset.Y;
            Log.Information("Method 3 - BringIntoView result: {Offset}", newOffset);
            
            return newOffset;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calculating scroll target for maximize");
            return scrollViewer.Offset.Y;
        }
    }

    /// <summary>
    /// Calculates the absolute position of an element within the scroll content
    /// </summary>
    /// <param name="element">The element to find position for</param>
    /// <param name="scrollViewer">The scroll viewer containing the element</param>
    /// <returns>The absolute Y position in scroll content coordinates, or null if calculation fails</returns>
    private double? CalculateAbsolutePositionInScrollContent(Visual element, ScrollViewer scrollViewer)
    {
        try
        {
            double absoluteY = 0;
            var current = element;
            
            // Traverse up the visual tree until we reach the scroll viewer's content
            while (current != null && current != scrollViewer)
            {
                if (current is Control control && control.IsArrangeValid)
                {
                    absoluteY += control.Bounds.Y;
                    Log.Information("Added bounds Y: {Y} from {Type}, total: {Total}", 
                        control.Bounds.Y, control.GetType().Name, absoluteY);
                }
                
                current = current.GetVisualParent();
            }
            
            if (current == scrollViewer)
            {
                Log.Information("Calculated absolute position: {Position}", absoluteY);
                return absoluteY;
            }
            else
            {
                Log.Warning("Could not traverse to scroll viewer");
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calculating absolute position in scroll content");
            return null;
        }
    }
}
