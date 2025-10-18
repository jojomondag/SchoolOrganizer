using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Serilog;

namespace SchoolOrganizer.Src.Services;

/// <summary>
/// Service for handling smooth scroll animations and positioning
/// </summary>
public class ScrollAnimationService
{
    private DispatcherTimer? _scrollAnimationTimer;
    private readonly Dictionary<object, double> _preMaximizeScrollPositions = new();

    /// <summary>
    /// Cancels any ongoing scroll animation
    /// </summary>
    public void CancelScrollAnimation()
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
    /// Smoothly scrolls the scroll viewer to the specified vertical offset
    /// </summary>
    /// <param name="scrollViewer">The scroll viewer to animate</param>
    /// <param name="targetOffset">The target vertical offset to scroll to</param>
    /// <param name="durationMs">Duration of the scroll animation in milliseconds</param>
    public void SmoothScrollToVerticalOffset(ScrollViewer scrollViewer, double targetOffset, int durationMs = 300)
    {
        try
        {
            if (scrollViewer == null)
            {
                Log.Warning("ScrollViewer is null for smooth scrolling");
                return;
            }

            CancelScrollAnimation();

            var startOffset = scrollViewer.Offset.Y;
            var delta = targetOffset - startOffset;
            
            if (Math.Abs(delta) < 0.5 || durationMs <= 0)
            {
                scrollViewer.Offset = new Vector(scrollViewer.Offset.X, targetOffset);
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
                    scrollViewer.Offset = new Vector(scrollViewer.Offset.X, newOffset);

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
    /// Saves the current scroll position for a specific button
    /// </summary>
    public void SaveScrollPosition(object button, ScrollViewer scrollViewer)
    {
        if (button != null && scrollViewer != null)
        {
            _preMaximizeScrollPositions[button] = scrollViewer.Offset.Y;
        }
    }

    /// <summary>
    /// Restores the saved scroll position for a specific button
    /// </summary>
    public void RestoreScrollPosition(object button, ScrollViewer scrollViewer)
    {
        if (button != null && _preMaximizeScrollPositions.TryGetValue(button, out var savedOffset))
        {
            SmoothScrollToVerticalOffset(scrollViewer, savedOffset);
            _preMaximizeScrollPositions.Remove(button);
        }
    }

    /// <summary>
    /// Calculates the target scroll offset to position an element at the top
    /// </summary>
    public double CalculateScrollTargetForElement(Visual element, ScrollViewer scrollViewer)
    {
        try
        {
            var currentOffset = scrollViewer.Offset.Y;
            
            // Try using TranslatePoint (viewport-relative coordinates)
            var elementPosition = element.TranslatePoint(new Point(0, 0), scrollViewer);
            if (elementPosition.HasValue)
            {
                var targetOffset = currentOffset + elementPosition.Value.Y;
                
                // Clamp to valid scroll range
                var maxScroll = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
                return Math.Max(0, Math.Min(targetOffset, maxScroll));
            }
            
            // Fallback: use absolute position calculation
            var absolutePosition = CalculateAbsolutePositionInScrollContent(element, scrollViewer);
            if (absolutePosition.HasValue)
            {
                var targetOffset = absolutePosition.Value;
                var maxScroll = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
                return Math.Max(0, Math.Min(targetOffset, maxScroll));
            }
            
            return currentOffset;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calculating scroll target for element");
            return scrollViewer.Offset.Y;
        }
    }

    /// <summary>
    /// Calculates the absolute position of an element within the scroll content
    /// </summary>
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
                }
                
                current = current.GetVisualParent();
            }
            
            return current == scrollViewer ? absoluteY : null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calculating absolute position in scroll content");
            return null;
        }
    }
}
