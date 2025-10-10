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
}

