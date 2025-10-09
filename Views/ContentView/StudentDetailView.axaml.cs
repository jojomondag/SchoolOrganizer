using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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
}
