using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Serilog;

namespace SchoolOrganizer.Src.Services;

/// <summary>
/// Service for managing UI panel visibility and layout
/// </summary>
public class PanelManagementService
{
    private bool _isExplorerCollapsed = false;

    /// <summary>
    /// Toggles the explorer panel visibility
    /// </summary>
    public void ToggleExplorerPanel(Grid mainGrid, Border explorerPanel, Border mainContentPanel, 
        Border separatorPanel, Button separatorToggleButton)
    {
        try
        {
            _isExplorerCollapsed = !_isExplorerCollapsed;
            var explorerColumn = mainGrid.ColumnDefinitions[0];
            var explorerTransform = explorerPanel?.RenderTransform as TranslateTransform;
            
            if (_isExplorerCollapsed)
            {
                CollapseExplorer(explorerColumn, explorerPanel, explorerTransform, mainContentPanel, 
                    separatorPanel, separatorToggleButton);
            }
            else
            {
                ExpandExplorer(explorerColumn, explorerPanel, explorerTransform, mainContentPanel, 
                    separatorPanel, separatorToggleButton);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error toggling explorer panel");
        }
    }

    /// <summary>
    /// Collapses the explorer panel
    /// </summary>
    private void CollapseExplorer(ColumnDefinition explorerColumn, Border? explorerPanel, 
        TranslateTransform? explorerTransform, Border? mainContentPanel, Border? separatorPanel, 
        Button? separatorToggleButton)
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
            separatorPanel.Width = 16;
            separatorPanel.HorizontalAlignment = HorizontalAlignment.Left;
        }
        
        // Expand main content to fill space
        if (mainContentPanel != null)
        {
            Grid.SetColumnSpan(mainContentPanel, 3);
            Grid.SetColumn(mainContentPanel, 0);
            mainContentPanel.Margin = new Thickness(16, 0, 0, 0);
        }
        
        // Update separator button chevron
        UpdateSeparatorButton(separatorToggleButton, "›");
    }

    /// <summary>
    /// Expands the explorer panel
    /// </summary>
    private void ExpandExplorer(ColumnDefinition explorerColumn, Border? explorerPanel, 
        TranslateTransform? explorerTransform, Border? mainContentPanel, Border? separatorPanel, 
        Button? separatorToggleButton)
    {
        // Expand the explorer column
        explorerColumn.Width = new GridLength(300, GridUnitType.Pixel);
        
        // Smooth slide in animation
        if (explorerTransform != null)
        {
            explorerTransform.X = 0;
        }
        
        // Move separator back to column 1
        if (separatorPanel != null)
        {
            Grid.SetColumn(separatorPanel, 1);
            separatorPanel.Width = double.NaN;
            separatorPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        
        // Reset main content to normal position
        if (mainContentPanel != null)
        {
            Grid.SetColumnSpan(mainContentPanel, 1);
            Grid.SetColumn(mainContentPanel, 2);
            mainContentPanel.Margin = new Thickness(0);
        }
        
        // Update separator button chevron
        UpdateSeparatorButton(separatorToggleButton, "‹");
    }

    /// <summary>
    /// Updates the separator button text
    /// </summary>
    private void UpdateSeparatorButton(Button? separatorToggleButton, string chevron)
    {
        if (separatorToggleButton?.Content is TextBlock textBlock)
        {
            textBlock.Text = chevron;
        }
    }

    /// <summary>
    /// Handles content maximization for file previews
    /// </summary>
    public void ToggleContentMaximization(Button button, ScrollViewer scrollViewer, 
        Material.Icons.Avalonia.MaterialIcon maximizerIcon, bool isCurrentlyMaximized)
    {
        try
        {
            if (isCurrentlyMaximized)
            {
                // Restore normal size
                scrollViewer.MaxHeight = 400;
                maximizerIcon.Kind = Material.Icons.MaterialIconKind.Fullscreen;
                RestoreContentElementSizes(scrollViewer, 300);
            }
            else
            {
                // Maximize content
                scrollViewer.MaxHeight = double.PositiveInfinity;
                maximizerIcon.Kind = Material.Icons.MaterialIconKind.FullscreenExit;
                RestoreContentElementSizes(scrollViewer, double.PositiveInfinity);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error toggling content maximization");
        }
    }

    /// <summary>
    /// Restores or sets content element sizes
    /// </summary>
    private void RestoreContentElementSizes(ScrollViewer scrollViewer, double maxHeight)
    {
        if (scrollViewer.Content is Grid contentGrid)
        {
            foreach (var child in contentGrid.Children)
            {
                if (child is Image image)
                {
                    image.MaxHeight = maxHeight;
                }
                else if (child is TextBlock textBlock)
                {
                    textBlock.MaxHeight = maxHeight;
                }
                else if (child is Control control && control.GetType().Name == "SyntaxHighlightedCodeViewer")
                {
                    control.MaxHeight = maxHeight;
                }
            }
        }
    }

    /// <summary>
    /// Gets the current explorer panel state
    /// </summary>
    public bool IsExplorerCollapsed => _isExplorerCollapsed;
}
