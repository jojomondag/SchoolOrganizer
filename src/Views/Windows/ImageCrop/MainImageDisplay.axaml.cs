using Avalonia.Controls;
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
        }
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
