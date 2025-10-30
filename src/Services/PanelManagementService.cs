using System;
using Avalonia.Controls;
using Serilog;

namespace SchoolOrganizer.Src.Services;

/// <summary>
/// Service for managing UI panel visibility and layout
/// </summary>
public class PanelManagementService
{
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
}
