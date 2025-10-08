using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media;
using Serilog;
using SchoolOrganizer.Models;

namespace SchoolOrganizer.Services;

/// <summary>
/// Service for handling scrolling to specific assignment groups in the Files Viewer
/// </summary>
public class FileViewerScrollService
{
    private readonly ScrollViewer? _scrollViewer;
    private readonly ItemsControl? _itemsControl;

    public FileViewerScrollService(ScrollViewer scrollViewer, ItemsControl itemsControl)
    {
        _scrollViewer = scrollViewer ?? throw new ArgumentNullException(nameof(scrollViewer));
        _itemsControl = itemsControl ?? throw new ArgumentNullException(nameof(itemsControl));
    }

    /// <summary>
    /// Scrolls to the assignment group that matches the selected folder
    /// </summary>
    /// <param name="selectedNode">The selected file tree node</param>
    /// <param name="allFilesGrouped">The collection of assignment groups</param>
    /// <returns>True if scrolling was successful, false otherwise</returns>
    public async Task<bool> ScrollToAssignmentGroupAsync(FileTreeNode selectedNode, ObservableCollection<AssignmentGroup> allFilesGrouped)
    {
        Log.Information("=== ScrollToAssignmentGroupAsync called ===");
        Log.Information("SelectedNode: {NodeName}, ScrollViewer null: {SVNull}, ItemsControl null: {ICNull}", 
            selectedNode?.Name ?? "null", _scrollViewer == null, _itemsControl == null);
        Log.Information("AllFilesGrouped count: {GroupCount}", allFilesGrouped?.Count ?? 0);
        
        if (selectedNode == null || _scrollViewer == null || _itemsControl == null)
        {
            Log.Warning("ScrollToAssignmentGroupAsync: Required parameters are null. SelectedNode: {SN}, ScrollViewer: {SV}, ItemsControl: {IC}", 
                selectedNode != null, _scrollViewer != null, _itemsControl != null);
            return false;
        }

        try
        {
            // Determine the target assignment name based on the selected node
            Log.Information("Determining target assignment name...");
            var targetAssignmentName = GetTargetAssignmentName(selectedNode);
            Log.Information("Target assignment name: {AssignmentName}", targetAssignmentName ?? "null");
            
            if (string.IsNullOrEmpty(targetAssignmentName))
            {
                Log.Warning("Could not determine target assignment name for node: {NodeName}", selectedNode.Name);
                return false;
            }

            // Log all available groups
            Log.Information("Available assignment groups:");
            if (allFilesGrouped != null)
            {
                for (int i = 0; i < allFilesGrouped.Count; i++)
                {
                    Log.Information("  [{Index}] {GroupName}", i, allFilesGrouped[i].AssignmentName);
                }
            }

            // Find the matching assignment group
            Log.Information("Looking for matching assignment group...");
            var targetGroup = allFilesGrouped?.FirstOrDefault(g => 
                string.Equals(g.AssignmentName, targetAssignmentName, StringComparison.OrdinalIgnoreCase));

            if (targetGroup == null)
            {
                Log.Warning("No assignment group found for assignment: {AssignmentName}", targetAssignmentName);
                // Try partial matching
                targetGroup = allFilesGrouped?.FirstOrDefault(g => 
                    g.AssignmentName.Contains(targetAssignmentName, StringComparison.OrdinalIgnoreCase) ||
                    targetAssignmentName.Contains(g.AssignmentName, StringComparison.OrdinalIgnoreCase));
                
                if (targetGroup != null)
                {
                    Log.Information("Found partial match: {PartialMatch}", targetGroup.AssignmentName);
                }
                else
                {
                    Log.Warning("No partial match found either");
                    return false;
                }
            }
            else
            {
                Log.Information("Found exact assignment group match: {GroupName}", targetGroup.AssignmentName);
            }

            // Get the index of the target group
            var groupIndex = allFilesGrouped!.IndexOf(targetGroup);
            Log.Information("Target group index: {Index}", groupIndex);
            
            if (groupIndex < 0)
            {
                Log.Warning("Could not find index for assignment group: {AssignmentName}", targetAssignmentName);
                return false;
            }

            // Wait for UI to be fully loaded and measured
            Log.Information("Waiting for UI to be ready...");
            await EnsureUIRendered();

            // Scroll to the group using different strategies
            Log.Information("Attempting scroll strategies...");
            var containerResult = await ScrollToGroupByContainer(groupIndex);
            Log.Information("Container strategy result: {Result}", containerResult);
            
            var calculationResult = false;
            var estimationResult = false;
            
            if (!containerResult)
            {
                calculationResult = await ScrollToGroupByCalculation(groupIndex, allFilesGrouped);
                Log.Information("Calculation strategy result: {Result}", calculationResult);
                
                if (!calculationResult)
                {
                    estimationResult = await ScrollToGroupByEstimation(groupIndex);
                    Log.Information("Estimation strategy result: {Result}", estimationResult);
                }
            }

            var scrolled = containerResult || calculationResult || estimationResult;

            if (scrolled)
            {
                Log.Information("=== Successfully scrolled to assignment group: {AssignmentName} at index {Index} ===", 
                    targetAssignmentName, groupIndex);
            }
            else
            {
                Log.Warning("=== Failed to scroll to assignment group: {AssignmentName} ===", targetAssignmentName);
            }

            return scrolled;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "=== Error during ScrollToAssignmentGroupAsync for node: {NodeName} ===", selectedNode?.Name);
            return false;
        }
    }

    /// <summary>
    /// Determines the target assignment name from the selected node
    /// </summary>
    private string GetTargetAssignmentName(FileTreeNode selectedNode)
    {
        // If the node has an assignment name, use it
        if (!string.IsNullOrEmpty(selectedNode.AssignmentName))
        {
            return selectedNode.AssignmentName;
        }

        // If it's a top-level directory, use the folder name as assignment name
        if (selectedNode.IsDirectory)
        {
            // Check if this is a top-level assignment folder
            var pathParts = selectedNode.RelativePath.Split('/', '\\');
            if (pathParts.Length == 1)
            {
                return selectedNode.Name;
            }
            else if (pathParts.Length > 1)
            {
                return pathParts[0]; // Use the root assignment folder name
            }
        }

        // Fallback: try to extract from relative path
        if (!string.IsNullOrEmpty(selectedNode.RelativePath))
        {
            var pathParts = selectedNode.RelativePath.Split('/', '\\');
            if (pathParts.Length > 0 && !string.IsNullOrEmpty(pathParts[0]))
            {
                return pathParts[0];
            }
        }

        return selectedNode.Name;
    }

    /// <summary>
    /// Strategy 1: Scroll using container generation (most accurate)
    /// </summary>
    private async Task<bool> ScrollToGroupByContainer(int groupIndex)
    {
        try
        {
            var result = await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // Get the container for the specific item
                var container = _itemsControl?.ContainerFromIndex(groupIndex);
                Log.Information("Container for index {Index}: {ContainerType}", groupIndex, container?.GetType().Name ?? "null");
                
                if (container is Control containerControl)
                {
                    Log.Information("Found container control, getting bounds...");
                    
                    // Get the bounds of the container relative to the ScrollViewer
                    var containerBounds = containerControl.Bounds;
                    Log.Information("Container bounds: {Bounds}", containerBounds);
                    
                    // Use BringIntoView for precise positioning
                    // This will automatically scroll to show the container at the top
                    Log.Information("Using BringIntoView for precise positioning");
                    containerControl.BringIntoView();
                    
                    // Give the scroll animation time to complete
                    await Task.Delay(50);
                    
                    // Fine-tune the position to align perfectly at the top
                    if (_scrollViewer != null)
                    {
                        var currentOffset = _scrollViewer.Offset.Y;
                        Log.Information("Current scroll offset after BringIntoView: {Offset}", currentOffset);
                        
                        // Adjust slightly to align the header perfectly at the top with a small margin
                        var adjustedOffset = Math.Max(0, currentOffset - 5); // 5px margin from top
                        _scrollViewer.Offset = new Avalonia.Vector(0, adjustedOffset);
                        Log.Information("Final adjusted scroll offset: {Offset}", adjustedOffset);
                    }
                    
                    return true;
                }
                else
                {
                    Log.Warning("Container is not a Control or is null");
                }
                return false;
            });

            return result;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "ScrollToGroupByContainer failed for index {Index}", groupIndex);
            return false;
        }
    }

    /// <summary>
    /// Strategy 2: Scroll by calculating cumulative heights of preceding groups using actual rendered elements
    /// </summary>
    private async Task<bool> ScrollToGroupByCalculation(int groupIndex, ObservableCollection<AssignmentGroup> allFilesGrouped)
    {
        try
        {
            var result = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                double cumulativeHeight = 0;
                
                Log.Information("Starting height calculation for {GroupCount} groups", allFilesGrouped.Count);
                
                // Try to get actual heights from rendered containers
                for (int i = 0; i < groupIndex && i < allFilesGrouped.Count; i++)
                {
                    var container = _itemsControl?.ContainerFromIndex(i);
                    if (container is Control containerControl && containerControl.IsArrangeValid)
                    {
                        // Use actual rendered height
                        var actualHeight = containerControl.Bounds.Height;
                        cumulativeHeight += actualHeight;
                        Log.Information("Group {Index}: Using actual height {Height}", i, actualHeight);
                    }
                    else
                    {
                        // Fallback to estimation
                        var group = allFilesGrouped[i];
                        const double groupHeaderHeight = 60;
                        const double fileItemBaseHeight = 100; // Base height per file
                        const double fileContentHeight = 200; // Estimated height for file content preview
                        const double groupMargin = 20;
                        
                        var estimatedHeight = groupHeaderHeight + 
                                            (group.Files.Count * (fileItemBaseHeight + fileContentHeight)) + 
                                            groupMargin;
                        cumulativeHeight += estimatedHeight;
                        Log.Information("Group {Index}: Using estimated height {Height} (files: {FileCount})", 
                            i, estimatedHeight, group.Files.Count);
                    }
                }

                // Add the margin from the ItemsControl itself (10px from XAML)
                cumulativeHeight += 10;
                
                // Leave a small margin at the top for perfect alignment
                cumulativeHeight = Math.Max(0, cumulativeHeight - 10);

                Log.Information("Total calculated height: {Height}", cumulativeHeight);

                // Scroll to the calculated position
                if (_scrollViewer != null)
                {
                    _scrollViewer.Offset = new Avalonia.Vector(0, cumulativeHeight);
                    Log.Information("Scroll offset set to calculated position: {Position}", cumulativeHeight);
                    return true;
                }
                
                return false;
            });

            return result;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "ScrollToGroupByCalculation failed for index {Index}", groupIndex);
            return false;
        }
    }

    /// <summary>
    /// Strategy 3: Scroll by estimation (fallback)
    /// </summary>
    private async Task<bool> ScrollToGroupByEstimation(int groupIndex)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Simple estimation: assume each group takes roughly 300 pixels
                const double estimatedGroupHeight = 300;
                var estimatedOffset = groupIndex * estimatedGroupHeight;

                if (_scrollViewer != null)
                {
                    _scrollViewer.Offset = new Avalonia.Vector(0, estimatedOffset);
                }
            });

            Log.Debug("Used estimation scroll to position: {Position} for group index {Index}", 
                groupIndex * 300, groupIndex);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "ScrollToGroupByEstimation failed for index {Index}", groupIndex);
            return false;
        }
    }

    /// <summary>
    /// Scrolls to the top of the files viewer
    /// </summary>
    public async Task ScrollToTopAsync()
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_scrollViewer != null)
                {
                    _scrollViewer.Offset = new Avalonia.Vector(0, 0);
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error scrolling to top");
        }
    }

    /// <summary>
    /// Gets the current scroll position
    /// </summary>
    public double GetCurrentScrollOffset()
    {
        return _scrollViewer?.Offset.Y ?? 0;
    }

    /// <summary>
    /// Ensures the UI is fully rendered and measured before scrolling
    /// </summary>
    private async Task EnsureUIRendered()
    {
        // Allow UI to render content
        await Task.Delay(50);
        
        // Force layout updates
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _itemsControl?.UpdateLayout();
            _scrollViewer?.UpdateLayout();
        }, Avalonia.Threading.DispatcherPriority.Render);
        
        // Give a bit more time for complex content to render
        await Task.Delay(100);
        
        Log.Information("UI rendering completed");
    }
}
