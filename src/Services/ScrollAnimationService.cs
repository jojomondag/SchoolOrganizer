using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Serilog;

namespace SchoolOrganizer.Src.Services;

/// <summary>
/// Service for handling smooth scroll animations and positioning
/// </summary>
public class ScrollAnimationService
{
    private DispatcherTimer? _scrollAnimationTimer;

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
}
