using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;

namespace SchoolOrganizer.Src.Views.Windows.ImageCrop;

public partial class MainImageDisplay : UserControl
{
    public event EventHandler? ImageAreaClicked;

    public MainImageDisplay()
    {
        InitializeComponent();
        InitializeEventHandlers();
        InitializeClipGeometry();
    }

    private void InitializeEventHandlers()
    {
        var mainImageArea = this.FindControl<Border>("MainImageArea");
        if (mainImageArea != null)
        {
            mainImageArea.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(mainImageArea).Properties.IsLeftButtonPressed)
                {
                    ImageAreaClicked?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
            };

            // Add hover handlers for showing hint overlay
            mainImageArea.PointerEntered += OnMainImageAreaPointerEntered;
            mainImageArea.PointerExited += OnMainImageAreaPointerExited;
        }

        // Also add hover handlers to CropOverlay to catch hover events over gray area
        var cropOverlay = GetCropOverlay();
        if (cropOverlay != null)
        {
            cropOverlay.PointerEntered += OnCropOverlayPointerEntered;
            cropOverlay.PointerExited += OnCropOverlayPointerExited;
        }
    }

    private void OnMainImageAreaPointerEntered(object? sender, PointerEventArgs e)
    {
        ShowHoverHintIfNeeded();
    }

    private void OnMainImageAreaPointerExited(object? sender, PointerEventArgs e)
    {
        HideHoverHint();
    }

    private void OnCropOverlayPointerEntered(object? sender, PointerEventArgs e)
    {
        // Only show hint if not hovering over crop selection area
        var selectionGroup = GetSelectionGroup();
        var cropOverlay = GetCropOverlay();
        if (selectionGroup != null && cropOverlay != null)
        {
            var point = e.GetPosition(cropOverlay);
            var selectionBounds = GetSelectionBounds();
            
            // Check if pointer is outside the crop selection circle
            if (selectionBounds.HasValue)
            {
                var center = new Point(selectionBounds.Value.X + selectionBounds.Value.Width / 2,
                                               selectionBounds.Value.Y + selectionBounds.Value.Height / 2);
                var distance = Math.Sqrt(Math.Pow(point.X - center.X, 2) + Math.Pow(point.Y - center.Y, 2));
                var radius = selectionBounds.Value.Width / 2;
                
                // Show hint only if pointer is outside the crop selection (gray area)
                if (distance > radius + 10) // Add small margin to avoid showing hint when near edge
                {
                    ShowHoverHintIfNeeded();
                }
            }
            else
            {
                ShowHoverHintIfNeeded();
            }
        }
        else
        {
            ShowHoverHintIfNeeded();
        }
    }

    private void OnCropOverlayPointerExited(object? sender, PointerEventArgs e)
    {
        HideHoverHint();
    }

    private void ShowHoverHintIfNeeded()
    {
        // Show hint overlay only when image is loaded (BackgroundPattern is hidden)
        var backgroundPattern = GetBackgroundPattern();
        var hoverHintOverlay = GetHoverHintOverlay();
        
        if (backgroundPattern != null && hoverHintOverlay != null && !backgroundPattern.IsVisible)
        {
            hoverHintOverlay.IsVisible = true;
        }
    }

    private void HideHoverHint()
    {
        var hoverHintOverlay = GetHoverHintOverlay();
        if (hoverHintOverlay != null)
        {
            hoverHintOverlay.IsVisible = false;
        }
    }

    private Rect? GetSelectionBounds()
    {
        var selectionGroup = GetSelectionGroup();
        var cropOverlay = GetCropOverlay();
        if (selectionGroup == null || cropOverlay == null)
            return null;

        var topLeft = selectionGroup.TranslatePoint(new Point(0, 0), cropOverlay);
        if (!topLeft.HasValue)
            return null;

        var bounds = selectionGroup.Bounds;
        return new Rect(topLeft.Value, bounds.Size);
    }

    private void InitializeClipGeometry()
    {
        var imageContainer = GetImageContainer();
        if (imageContainer != null)
        {
            imageContainer.SizeChanged += (s, e) =>
            {
                if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
                {
                    if (imageContainer.Clip is RectangleGeometry rectGeom)
                    {
                        rectGeom.Rect = new Avalonia.Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
                    }
                }
            };
        }
    }

    public Border? GetMainImageArea() => this.FindControl<Border>("MainImageArea");
    public Grid? GetImageContainer() => this.FindControl<Grid>("ImageContainer");
    public Grid? GetBackgroundPattern() => this.FindControl<Grid>("BackgroundPattern");
    public Image? GetMainImage() => this.FindControl<Image>("MainImage");
    public Grid? GetCropOverlay() => this.FindControl<Grid>("CropOverlay");
    public AvaloniaPath? GetOverlayCutout() => this.FindControl<AvaloniaPath>("OverlayCutout");
    public Grid? GetSelectionGroup() => this.FindControl<Grid>("SelectionGroup");
    public Border? GetCropSelection() => this.FindControl<Border>("CropSelection");
    public Canvas? GetHandlesLayer() => this.FindControl<Canvas>("HandlesLayer");
    public Border? GetHandleTopLeft() => this.FindControl<Border>("HandleTopLeft");
    public Border? GetHandleTopRight() => this.FindControl<Border>("HandleTopRight");
    public Border? GetHandleBottomLeft() => this.FindControl<Border>("HandleBottomLeft");
    public Border? GetHandleBottomRight() => this.FindControl<Border>("HandleBottomRight");
    public Border? GetHoverHintOverlay() => this.FindControl<Border>("HoverHintOverlay");

    /// <summary>
    /// Clears the main image display and resets it to the initial state where users can select a new image
    /// </summary>
    public void ClearImage()
    {
        var mainImage = GetMainImage();
        var backgroundPattern = GetBackgroundPattern();
        var cropOverlay = GetCropOverlay();

        if (mainImage != null)
        {
            mainImage.Source = null;
            mainImage.IsVisible = false;
        }

        if (backgroundPattern != null)
        {
            backgroundPattern.IsVisible = true;
        }

        if (cropOverlay != null)
        {
            cropOverlay.IsVisible = false;
        }
    }
}
