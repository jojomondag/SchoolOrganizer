using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ImageSelector;

public partial class ImageSelectorView : UserControl
{
    private Bitmap? _originalBitmap;
    private bool _isDragging = false;
    private bool _isResizing = false;
    private bool _isRotating = false;
    private Point _lastPointerPosition;
    private string? _resizeHandle;
    private string? _rotateHandle;
    private Rect _cropRect;
    private Size _imageDisplaySize;
    private Point _imageDisplayOffset;
    private Point _dragStartPointer;
    private Rect _dragStartRect;
    private double _rotationAngleDegrees = 0;
    private double _rotateStartPointerAngle;
    private double _initialRotationAngle;

    // Deferred restore support
    private object? _pendingCropSettingsToRestore;
    private bool _attemptRestoreOnNextLayout;

    // API for external integration
    public Func<Task<string?>>? SavePathProvider { get; set; }
    public event EventHandler<string>? ImageSaved;
    public event EventHandler<IStorageFile>? OriginalImageSelected;

    public ImageSelectorView()
    {
        InitializeComponent();
        InitializeEventHandlers();
    }

    private void InitializeEventHandlers()
    {
        // Handle clicking on the main image area to select image
        MainImageArea.PointerPressed += MainImageArea_PointerPressed;
        
        // Handle pointer events for selection group (border + handles)
        SelectionGroup.PointerPressed += CropSelection_PointerPressed;
        SelectionGroup.PointerMoved += CropSelection_PointerMoved;
        SelectionGroup.PointerReleased += CropSelection_PointerReleased;
        SelectionGroup.PointerEntered += (s, e) => { if (!_isRotating) ShowRotateHandles(false); };
        SelectionGroup.PointerExited += (s, e) => { if (!_isRotating) ShowRotateHandles(false); };

        // Handle corner handles
        var handles = new[] { 
            (HandleTopLeft, "top-left"), (HandleTopRight, "top-right"), 
            (HandleBottomLeft, "bottom-left"), (HandleBottomRight, "bottom-right") 
        };
        
        foreach (var (handle, position) in handles)
        {
            handle.PointerPressed += (s, e) => StartResize(e, position);
            handle.PointerReleased += (s, e) => EndResize(e);
        }

        // Rotate handles
        var rotateHandles = new[] {
            (RotateTopLeft, "top-left"), (RotateTopRight, "top-right"),
            (RotateBottomLeft, "bottom-left"), (RotateBottomRight, "bottom-right")
        };
        foreach (var (handle, position) in rotateHandles)
        {
            handle.PointerPressed += (s, e) => StartRotate(e, position);
            handle.PointerReleased += (s, e) => EndRotate(e);
            handle.PointerEntered += (s, e) => { handle.IsVisible = true; };
        }

        // Handle pointer events on the overlay grid for handle dragging
        CropOverlay.PointerMoved += CropOverlay_PointerMoved;
        CropOverlay.PointerReleased += CropOverlay_PointerReleased;
        CropOverlay.PointerExited += (s, e) => ShowRotateHandles(false);
        
        // Handle size changes to reinitialize crop selection
        CropOverlay.SizeChanged += CropOverlay_SizeChanged;
        // Adjust main content column width when the container resizes
        ContentGrid.SizeChanged += ContentGrid_SizeChanged;
    }

    private void CropOverlay_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_originalBitmap != null && CropOverlay.IsVisible)
        {
            UpdateCropSelectionSize();
            // If a restore was deferred due to missing layout info, try now
            if (_attemptRestoreOnNextLayout && _pendingCropSettingsToRestore != null)
            {
                if (TryRestoreCropSettings(_pendingCropSettingsToRestore))
                {
                    _attemptRestoreOnNextLayout = false;
                    _pendingCropSettingsToRestore = null;
                    ApplyCropRectToUI();
                    UpdatePreview();
                }
            }
        }
    }

    private async void MainImageArea_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only handle left button press
        if (e.GetCurrentPoint(MainImageArea).Properties.IsLeftButtonPressed)
        {
            // If no image is loaded, allow clicking anywhere in the main area
            if (_originalBitmap == null)
            {
                await SelectImage();
                e.Handled = true;
            }
            // If image is loaded, only respond to clicks on the background pattern (not on the image itself)
            else if (BackgroundPattern.IsVisible)
            {
                await SelectImage();
                e.Handled = true;
            }
        }
    }

    private async Task SelectImage()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Image Files")
                {
                    Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif" }
                }
            }
        });

        if (files.Count > 0)
        {
            OriginalImageSelected?.Invoke(this, files[0]);
            await LoadImage(files[0]);
        }
    }

    private async Task LoadImage(IStorageFile file)
    {
        using var stream = await file.OpenReadAsync();
        var bmp = new Bitmap(stream);
        await SetOriginalBitmapAsync(bmp);
    }

    private void ContentGrid_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateLeftColumnWidth();
    }

    private void UpdateLeftColumnWidth()
    {
        if (_originalBitmap == null) return;
        if (ContentGrid.ColumnDefinitions.Count < 2) return;

        // Reserve space for the fixed sidebar width (320px) even before layout updates ActualWidth
        var rightCol = ContentGrid.ColumnDefinitions[1];
        double sidebarWidth = rightCol.Width.IsStar ? rightCol.ActualWidth : rightCol.Width.Value;
        if (sidebarWidth <= 1)
        {
            sidebarWidth = 320; // fallback to declared width
        }
        // Clamp by window width so layout never overflows horizontally
        var root = TopLevel.GetTopLevel(this);
        var containerWidth = root?.ClientSize.Width ?? ContentGrid.Bounds.Width;
        var contentMarginH = ContentGrid.Margin.Left + ContentGrid.Margin.Right;
        var maxTotalWidth = Math.Max(0, containerWidth - contentMarginH);
        var totalWidth = Math.Min(ContentGrid.Bounds.Width, maxTotalWidth);
        var availableWidth = Math.Max(0, totalWidth - sidebarWidth);
        // Account for spacing between image area and sidebar (right margin on MainImageArea)
        var gapWidth = (MainImageArea != null ? (MainImageArea.Margin.Left + MainImageArea.Margin.Right) : 0);
        availableWidth = Math.Max(0, availableWidth - gapWidth);
        var availableHeight = ContentGrid.Bounds.Height;
        if (availableWidth <= 0 || availableHeight <= 0) return;

        var imageAspect = (double)_originalBitmap.PixelSize.Width / _originalBitmap.PixelSize.Height;
        var desiredWidth = Math.Min(availableWidth, availableHeight * imageAspect);
        var desiredHeight = Math.Min(availableHeight, desiredWidth / imageAspect);

        // Keep left column flexible but constrain content via explicit size to avoid pushing sidebar
        ContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);

        // Also set the MainImageArea to the exact display size so there's no internal whitespace
        if (MainImageArea != null)
        {
            MainImageArea.Width = desiredWidth;
            MainImageArea.Height = desiredHeight;
        }

        AdjustWindowSizeForImage();
    }

    // Public API: preload image from a local file path (used by host app)
    public async Task LoadImageFromPathAsync(string path)
    {
        if (!File.Exists(path)) return;
        await using var fs = File.OpenRead(path);
        var bmp = new Bitmap(fs);
        await SetOriginalBitmapAsync(bmp);
    }

    // Public API: preload image with saved crop settings
    public async Task LoadImageFromPathWithCropSettingsAsync(string path, object? cropSettings)
    {
        if (!File.Exists(path)) return;
        await using var fs = File.OpenRead(path);
        var bmp = new Bitmap(fs);
        await SetOriginalBitmapAsync(bmp, cropSettings);
    }

    private async Task SetOriginalBitmapAsync(Bitmap bmp, object? cropSettings = null)
    {
        _originalBitmap?.Dispose();
        _originalBitmap = bmp;
        
        MainImage.Source = _originalBitmap;
        MainImage.IsVisible = true;
        BackgroundPattern.IsVisible = false;
        CropOverlay.IsVisible = true;
        BackButton.Opacity = 1;
        BackButton.IsHitTestVisible = true;
        UpdateLeftColumnWidth();
        MainImageArea.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        MainImageArea.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        
        await Task.Delay(100);
        
        // If crop settings are provided, restore them; otherwise use defaults
        if (cropSettings != null)
        {
            System.Diagnostics.Debug.WriteLine("SetOriginalBitmapAsync: Crop settings provided, attempting to restore");
            if (TryRestoreCropSettings(cropSettings))
            {
                System.Diagnostics.Debug.WriteLine("SetOriginalBitmapAsync: Crop settings restored successfully");
                ApplyCropRectToUI();
                UpdatePreview();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("SetOriginalBitmapAsync: Failed to restore crop settings, using defaults");
                // Defer restore until layout provides non-zero bounds
                _pendingCropSettingsToRestore = cropSettings;
                _attemptRestoreOnNextLayout = true;
                UpdateCropSelectionSize();
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("SetOriginalBitmapAsync: No crop settings provided, using defaults");
            UpdateCropSelectionSize();
        }
        
        ResetButton.IsEnabled = true;
        SaveButton.IsEnabled = true;
        
        if (_cropRect.Width > 0 && _cropRect.Height > 0 && _imageDisplaySize.Width > 0 && _imageDisplaySize.Height > 0)
        {
            UpdatePreview();
        }
    }

    // Public API: save a copy of the original (uncropped) bitmap to the given path
    public async Task<bool> SaveOriginalCopyAsync(string path)
    {
        if (_originalBitmap == null) return false;
        try
        {
            await using var stream = File.Create(path);
            _originalBitmap.Save(stream);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void AdjustWindowSizeForImage()
    {
        if (_originalBitmap == null) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window window) return;

        // Compute desired window size based on image area + sidebar + margins
        var sidebarWidth = 320; // fixed in XAML
        var contentMarginH = ContentGrid.Margin.Left + ContentGrid.Margin.Right; // typically 32
        var contentMarginV = ContentGrid.Margin.Top + ContentGrid.Margin.Bottom; // typically 32
        var gapWidth = MainImageArea.Margin.Left + MainImageArea.Margin.Right;   // includes 24 right gap

        var desiredWindowClientWidth = contentMarginH + MainImageArea.Width + gapWidth + sidebarWidth;
        var desiredWindowClientHeight = contentMarginV + Math.Max(MainImageArea.Height, 300); // ensure room for sidebar content

        // Clamp to working area
        var work = topLevel.Screens?.Primary?.WorkingArea;
        if (work.HasValue)
        {
            desiredWindowClientWidth = Math.Min(desiredWindowClientWidth, work.Value.Width - 40);
            desiredWindowClientHeight = Math.Min(desiredWindowClientHeight, work.Value.Height - 40);
        }

        // Respect min sizes
        desiredWindowClientWidth = Math.Max(window.MinWidth, desiredWindowClientWidth);
        desiredWindowClientHeight = Math.Max(window.MinHeight, desiredWindowClientHeight);

        window.Width = desiredWindowClientWidth;
        window.Height = desiredWindowClientHeight;
    }

    private void UpdateCropSelectionSize()
    {
        if (_originalBitmap == null) return;

        var imageBounds = CropOverlay.Bounds;
        
        if (imageBounds.Width == 0 || imageBounds.Height == 0)
        {
            CropSelection.Width = 200;
            CropSelection.Height = 200;
            _imageDisplaySize = new Size(400, 300);
            _imageDisplayOffset = new Point(100, 50);
            _cropRect = new Rect(200, 150, 200, 200);
            return;
        }
        
        var imageAspectRatio = (double)_originalBitmap.PixelSize.Width / _originalBitmap.PixelSize.Height;
        var containerAspectRatio = imageBounds.Width / imageBounds.Height;
        
        if (imageAspectRatio > containerAspectRatio)
        {
            _imageDisplaySize = new Size(imageBounds.Width, imageBounds.Width / imageAspectRatio);
            _imageDisplayOffset = new Point(0, (imageBounds.Height - _imageDisplaySize.Height) / 2);
        }
        else
        {
            _imageDisplaySize = new Size(imageBounds.Height * imageAspectRatio, imageBounds.Height);
            _imageDisplayOffset = new Point((imageBounds.Width - _imageDisplaySize.Width) / 2, 0);
        }

        var cropSize = Math.Min(_imageDisplaySize.Width, _imageDisplaySize.Height) * 0.6;
        CropSelection.Width = cropSize;
        CropSelection.Height = cropSize;
        
        var cropX = _imageDisplayOffset.X + (_imageDisplaySize.Width - cropSize) / 2;
        var cropY = _imageDisplayOffset.Y + (_imageDisplaySize.Height - cropSize) / 2;
        _cropRect = new Rect(cropX, cropY, cropSize, cropSize);
        _rotationAngleDegrees = 0;
        ApplyCropRectToUI();
    }

    private void ApplyCropRectToUI()
    {
        // Snap to device pixels to avoid 1px seams/flicker
        var x = Math.Floor(_cropRect.X);
        var y = Math.Floor(_cropRect.Y);
        var right = Math.Ceiling(_cropRect.X + _cropRect.Width);
        var bottom = Math.Ceiling(_cropRect.Y + _cropRect.Height);
        var width = Math.Max(0, right - x);
        var height = Math.Max(0, bottom - y);

        SelectionGroup.Width = width;
        SelectionGroup.Height = height;
        SelectionGroup.Margin = new Thickness(x, y, 0, 0);
        CropSelection.Width = width;
        CropSelection.Height = height;

        // Apply current rotation to the selection element
        UpdateRotationVisuals();

        // Update the cutout geometry for the dark overlay
        UpdateCropCutout();
    }
    
    private void UpdateCropCutout()
    {
        // Get the bounds of the overlay area
        var overlayBounds = CropOverlay.Bounds;
        if (overlayBounds.Width == 0 || overlayBounds.Height == 0)
            return;

        // Outer overlay should match the displayed image rectangle, not the full overlay bounds
        // This prevents dimming the letterboxed side/top/bottom areas outside the image
        var imageLeft = Math.Floor(_imageDisplayOffset.X);
        var imageTop = Math.Floor(_imageDisplayOffset.Y);
        var imageRight = Math.Ceiling(_imageDisplayOffset.X + _imageDisplaySize.Width);
        var imageBottom = Math.Ceiling(_imageDisplayOffset.Y + _imageDisplaySize.Height);
        var outerRect = new Rect(
            imageLeft,
            imageTop,
            Math.Max(0, imageRight - imageLeft),
            Math.Max(0, imageBottom - imageTop));

        // Inner crop rectangle snapped to device pixels
        var innerLeft = Math.Floor(_cropRect.X);
        var innerTop = Math.Floor(_cropRect.Y);
        var innerRight = Math.Ceiling(_cropRect.X + _cropRect.Width);
        var innerBottom = Math.Ceiling(_cropRect.Y + _cropRect.Height);
        var innerRect = new Rect(innerLeft, innerTop, Math.Max(0, innerRight - innerLeft), Math.Max(0, innerBottom - innerTop));

        // Build even-odd geometry: full rect minus inner rect
        var group = new GeometryGroup
        {
            FillRule = FillRule.EvenOdd,
        };
        group.Children.Add(new RectangleGeometry(outerRect));
        var innerGeom = new RectangleGeometry(innerRect);
        if (Math.Abs(_rotationAngleDegrees) > 0.01)
        {
            var cx = innerRect.X + innerRect.Width / 2;
            var cy = innerRect.Y + innerRect.Height / 2;
            innerGeom.Transform = new RotateTransform(_rotationAngleDegrees, cx, cy);
        }
        group.Children.Add(innerGeom);

        if (OverlayCutout != null)
        {
            OverlayCutout.Data = group;
        }
    }

    private void CropSelection_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(CropSelection).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _lastPointerPosition = e.GetPosition(CropOverlay);
            _dragStartPointer = _lastPointerPosition;
            _dragStartRect = _cropRect;
            e.Pointer.Capture(CropSelection);
            e.Handled = true;
        }
    }

    private void CropSelection_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
        {
            var currentPosition = e.GetPosition(CropOverlay);
            var deltaX = currentPosition.X - _dragStartPointer.X;
            var deltaY = currentPosition.Y - _dragStartPointer.Y;

            var newX = Math.Max(_imageDisplayOffset.X,
                Math.Min(_imageDisplayOffset.X + _imageDisplaySize.Width - _dragStartRect.Width,
                    _dragStartRect.X + deltaX));
            var newY = Math.Max(_imageDisplayOffset.Y,
                Math.Min(_imageDisplayOffset.Y + _imageDisplaySize.Height - _dragStartRect.Height,
                    _dragStartRect.Y + deltaY));

            _cropRect = new Rect(newX, newY, _dragStartRect.Width, _dragStartRect.Height);
            ApplyCropRectToUI();
            UpdatePreview();
            e.Handled = true;
        }
    }

    private void CropSelection_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void StartResize(PointerPressedEventArgs e, string handle)
    {
        if (e.GetCurrentPoint(CropOverlay).Properties.IsLeftButtonPressed)
        {
            _isResizing = true;
            _resizeHandle = handle;
            _lastPointerPosition = e.GetPosition(CropOverlay);
            _dragStartPointer = _lastPointerPosition;
            _dragStartRect = _cropRect;
            e.Pointer.Capture(CropOverlay);
            e.Handled = true;
        }
    }

    private void EndResize(PointerReleasedEventArgs e)
    {
        _isResizing = false;
        _resizeHandle = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void CropOverlay_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isRotating)
        {
            // Only rotate while left mouse button is held down
            if (!e.GetCurrentPoint(CropOverlay).Properties.IsLeftButtonPressed)
            {
                _isRotating = false;
                _rotateHandle = null;
                e.Pointer.Capture(null);
                ShowRotateHandles(false);
                e.Handled = true;
                return;
            }
            var current = e.GetPosition(CropOverlay);
            var center = new Point(_cropRect.X + _cropRect.Width / 2, _cropRect.Y + _cropRect.Height / 2);
            var angleNow = ComputeAngleDegrees(center, current);
            var delta = angleNow - _rotateStartPointerAngle;
            _rotationAngleDegrees = NormalizeAngle(_initialRotationAngle + delta);
            UpdateRotationVisuals();
            UpdateCropCutout();
            UpdatePreview();
            e.Handled = true;
            return;
        }
        if (_isResizing && !string.IsNullOrEmpty(_resizeHandle))
        {
            var currentPosition = e.GetPosition(CropOverlay);
            const double minSize = 50;

            // Scale uniformly from the selection center based on pointer distance change
            var centerX = _dragStartRect.X + _dragStartRect.Width / 2;
            var centerY = _dragStartRect.Y + _dragStartRect.Height / 2;
            var startVecX = _dragStartPointer.X - centerX;
            var startVecY = _dragStartPointer.Y - centerY;
            var curVecX = currentPosition.X - centerX;
            var curVecY = currentPosition.Y - centerY;

            var startLen = Math.Sqrt(startVecX * startVecX + startVecY * startVecY);
            var curLen = Math.Sqrt(curVecX * curVecX + curVecY * curVecY);
            if (startLen < 1) startLen = 1; // avoid division by zero

            var scale = curLen / startLen; // outward -> >1, inward -> <1

            var halfStart = _dragStartRect.Width / 2.0;
            var halfNew = Math.Max(minSize / 2.0, halfStart * scale);

            // Clamp to image display bounds while keeping center fixed
            var imageLeft = _imageDisplayOffset.X;
            var imageTop = _imageDisplayOffset.Y;
            var imageRight = _imageDisplayOffset.X + _imageDisplaySize.Width;
            var imageBottom = _imageDisplayOffset.Y + _imageDisplaySize.Height;

            var maxHalfX = Math.Min(centerX - imageLeft, imageRight - centerX);
            var maxHalfY = Math.Min(centerY - imageTop, imageBottom - centerY);
            var maxHalf = Math.Max(1, Math.Min(maxHalfX, maxHalfY));
            halfNew = Math.Min(halfNew, maxHalf);

            var w = halfNew * 2.0;
            var h = w;
            var x = centerX - halfNew;
            var y = centerY - halfNew;

            _cropRect = new Rect(x, y, w, h);
            ApplyCropRectToUI();
            UpdatePreview();
            e.Handled = true;
        }
        else
        {
            var posOverlay = e.GetPosition(CropOverlay);
            var inside = _cropRect.Contains(posOverlay);
            if (inside)
            {
                ShowRotateHandles(false);
            }
            else
            {
                var posLocal = e.GetPosition(SelectionGroup);
                ShowOnlyNearestCornerRotateHandle(posLocal);
            }
        }
    }

    private void CropOverlay_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        EndResize(e);
        EndRotate(e);
    }

    private void UpdatePreview()
    {
        if (_originalBitmap == null || _cropRect.Width <= 0 || _cropRect.Height <= 0 || 
            _imageDisplaySize.Width <= 0 || _imageDisplaySize.Height <= 0)
        {
            PreviewImage.IsVisible = false;
            return;
        }
        
        var croppedBitmap = CropImage(_originalBitmap, _cropRect);
        PreviewImage.Source = croppedBitmap;
        PreviewImage.IsVisible = croppedBitmap != null;
    }

    private Bitmap CropImage(Bitmap source, Rect cropRect)
    {
        if (_imageDisplaySize.Width <= 0 || _imageDisplaySize.Height <= 0)
            return source;
        
        var scaleX = (double)source.PixelSize.Width / _imageDisplaySize.Width;
        var scaleY = (double)source.PixelSize.Height / _imageDisplaySize.Height;
        
        var imageCropX = (int)((cropRect.X - _imageDisplayOffset.X) * scaleX);
        var imageCropY = (int)((cropRect.Y - _imageDisplayOffset.Y) * scaleY);
        var imageCropWidth = (int)(cropRect.Width * scaleX);
        var imageCropHeight = (int)(cropRect.Height * scaleY);
        
        imageCropX = Math.Max(0, Math.Min(source.PixelSize.Width - 1, imageCropX));
        imageCropY = Math.Max(0, Math.Min(source.PixelSize.Height - 1, imageCropY));
        imageCropWidth = Math.Min(source.PixelSize.Width - imageCropX, imageCropWidth);
        imageCropHeight = Math.Min(source.PixelSize.Height - imageCropY, imageCropHeight);
        
        if (imageCropWidth <= 0 || imageCropHeight <= 0)
            return source;
            
        var renderTarget = new RenderTargetBitmap(new PixelSize(imageCropWidth, imageCropHeight));
        using var drawingContext = renderTarget.CreateDrawingContext();
        var destRect = new Rect(0, 0, imageCropWidth, imageCropHeight);

        if (Math.Abs(_rotationAngleDegrees) <= 0.01)
        {
            var sourceRect = new Rect(imageCropX, imageCropY, imageCropWidth, imageCropHeight);
            drawingContext.DrawImage(source, sourceRect, destRect);
            return renderTarget;
        }

        // Draw the rotated image and clip to the crop area
        using (drawingContext.PushClip(destRect))
        using (drawingContext.PushTransform(
            Matrix.CreateTranslation(-(imageCropX + imageCropWidth / 2.0), -(imageCropY + imageCropHeight / 2.0)) *
            Matrix.CreateRotation(_rotationAngleDegrees * Math.PI / 180.0) *
            Matrix.CreateTranslation(imageCropWidth / 2.0, imageCropHeight / 2.0)))
        {
            var fullSourceRect = new Rect(0, 0, source.PixelSize.Width, source.PixelSize.Height);
            var fullDestRect = new Rect(0, 0, source.PixelSize.Width, source.PixelSize.Height);
            drawingContext.DrawImage(source, fullSourceRect, fullDestRect);
        }

        return renderTarget;
    }

    private bool TryRestoreCropSettings(object? cropSettings)
    {
        if (cropSettings == null)
        {
            System.Diagnostics.Debug.WriteLine("TryRestoreCropSettings: cropSettings is null");
            return false;
        }

        if (_originalBitmap == null)
        {
            System.Diagnostics.Debug.WriteLine("TryRestoreCropSettings: no original bitmap loaded");
            return false;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"TryRestoreCropSettings: Attempting to restore crop settings of type {cropSettings.GetType().Name}");

            // Use reflection to get properties from the crop settings object
            var settingsType = cropSettings.GetType();

            var xProp = settingsType.GetProperty("X");
            var yProp = settingsType.GetProperty("Y");
            var widthProp = settingsType.GetProperty("Width");
            var heightProp = settingsType.GetProperty("Height");
            var rotationProp = settingsType.GetProperty("RotationAngle");
            var imageDisplayWidthProp = settingsType.GetProperty("ImageDisplayWidth");
            var imageDisplayHeightProp = settingsType.GetProperty("ImageDisplayHeight");
            var imageDisplayOffsetXProp = settingsType.GetProperty("ImageDisplayOffsetX");
            var imageDisplayOffsetYProp = settingsType.GetProperty("ImageDisplayOffsetY");

            if (xProp == null || yProp == null || widthProp == null || heightProp == null ||
                imageDisplayWidthProp == null || imageDisplayHeightProp == null ||
                imageDisplayOffsetXProp == null || imageDisplayOffsetYProp == null)
            {
                System.Diagnostics.Debug.WriteLine("TryRestoreCropSettings: Missing required properties");
                return false;
            }

            var savedX = Convert.ToDouble(xProp.GetValue(cropSettings));
            var savedY = Convert.ToDouble(yProp.GetValue(cropSettings));
            var savedWidth = Convert.ToDouble(widthProp.GetValue(cropSettings));
            var savedHeight = Convert.ToDouble(heightProp.GetValue(cropSettings));
            var savedRotation = rotationProp != null ? Convert.ToDouble(rotationProp.GetValue(cropSettings)) : 0.0;
            var savedDisplayWidth = Convert.ToDouble(imageDisplayWidthProp.GetValue(cropSettings));
            var savedDisplayHeight = Convert.ToDouble(imageDisplayHeightProp.GetValue(cropSettings));
            var savedOffsetX = Convert.ToDouble(imageDisplayOffsetXProp.GetValue(cropSettings));
            var savedOffsetY = Convert.ToDouble(imageDisplayOffsetYProp.GetValue(cropSettings));

            // Compute the CURRENT displayed image rect within the overlay, based on container size
            var imageBounds = CropOverlay.Bounds;
            if (imageBounds.Width <= 0 || imageBounds.Height <= 0)
            {
                System.Diagnostics.Debug.WriteLine("TryRestoreCropSettings: overlay bounds not ready");
                return false;
            }

            var imageAspectRatio = (double)_originalBitmap.PixelSize.Width / _originalBitmap.PixelSize.Height;
            var containerAspectRatio = imageBounds.Width / imageBounds.Height;

            Size currentDisplaySize;
            Point currentDisplayOffset;
            if (imageAspectRatio > containerAspectRatio)
            {
                currentDisplaySize = new Size(imageBounds.Width, imageBounds.Width / imageAspectRatio);
                currentDisplayOffset = new Point(0, (imageBounds.Height - currentDisplaySize.Height) / 2);
            }
            else
            {
                currentDisplaySize = new Size(imageBounds.Height * imageAspectRatio, imageBounds.Height);
                currentDisplayOffset = new Point((imageBounds.Width - currentDisplaySize.Width) / 2, 0);
            }

            // Scale saved crop to current display geometry
            var scaleX = savedDisplayWidth > 0 ? currentDisplaySize.Width / savedDisplayWidth : 1.0;
            var scaleY = savedDisplayHeight > 0 ? currentDisplaySize.Height / savedDisplayHeight : 1.0;

            var relX = savedX - savedOffsetX;
            var relY = savedY - savedOffsetY;

            var newX = currentDisplayOffset.X + relX * scaleX;
            var newY = currentDisplayOffset.Y + relY * scaleY;
            var newW = savedWidth * scaleX;
            var newH = savedHeight * scaleY;

            // Clamp to image display rect
            newW = Math.Max(1, Math.Min(newW, currentDisplaySize.Width));
            newH = Math.Max(1, Math.Min(newH, currentDisplaySize.Height));
            newX = Math.Max(currentDisplayOffset.X, Math.Min(currentDisplayOffset.X + currentDisplaySize.Width - newW, newX));
            newY = Math.Max(currentDisplayOffset.Y, Math.Min(currentDisplayOffset.Y + currentDisplaySize.Height - newH, newY));

            _imageDisplaySize = currentDisplaySize;
            _imageDisplayOffset = currentDisplayOffset;
            _cropRect = new Rect(newX, newY, newW, newH);
            _rotationAngleDegrees = savedRotation;

            System.Diagnostics.Debug.WriteLine("TryRestoreCropSettings: Successfully restored crop settings to current layout");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TryRestoreCropSettings: Error restoring crop settings: {ex.Message}");
            return false;
        }
    }

    // Public API: get current crop settings for saving
    public object? GetCurrentCropSettings()
    {
        if (_originalBitmap == null || _cropRect.Width <= 0 || _cropRect.Height <= 0)
            return null;
            
        return new
        {
            X = _cropRect.X,
            Y = _cropRect.Y,
            Width = _cropRect.Width,
            Height = _cropRect.Height,
            RotationAngle = _rotationAngleDegrees,
            ImageDisplayWidth = _imageDisplaySize.Width,
            ImageDisplayHeight = _imageDisplaySize.Height,
            ImageDisplayOffsetX = _imageDisplayOffset.X,
            ImageDisplayOffsetY = _imageDisplayOffset.Y
        };
    }

    private void StartRotate(PointerPressedEventArgs e, string handle)
    {
        if (e.GetCurrentPoint(CropOverlay).Properties.IsLeftButtonPressed)
        {
            _isRotating = true;
            _rotateHandle = handle;
            _dragStartPointer = e.GetPosition(CropOverlay);
            var center = new Point(_cropRect.X + _cropRect.Width / 2, _cropRect.Y + _cropRect.Height / 2);
            _rotateStartPointerAngle = ComputeAngleDegrees(center, _dragStartPointer);
            _initialRotationAngle = _rotationAngleDegrees;
            e.Pointer.Capture(CropOverlay);
            e.Handled = true;
        }
    }

    private void EndRotate(PointerReleasedEventArgs e)
    {
        _isRotating = false;
        _rotateHandle = null;
        e.Pointer.Capture(null);
        ShowRotateHandles(false);
        e.Handled = true;
    }

    private void UpdateRotationVisuals()
    {
        SelectionGroup.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        SelectionGroup.RenderTransform = new RotateTransform(_rotationAngleDegrees);
        PositionHandles();
    }

    private void PositionHandles()
    {
        // Corner resize handles at the corners (parent SelectionGroup rotation applies visually)
        Canvas.SetLeft(HandleTopLeft, -7);
        Canvas.SetTop(HandleTopLeft, -7);

        Canvas.SetLeft(HandleTopRight, SelectionGroup.Width - 7);
        Canvas.SetTop(HandleTopRight, -7);

        Canvas.SetLeft(HandleBottomLeft, -7);
        Canvas.SetTop(HandleBottomLeft, SelectionGroup.Height - 7);

        Canvas.SetLeft(HandleBottomRight, SelectionGroup.Width - 7);
        Canvas.SetTop(HandleBottomRight, SelectionGroup.Height - 7);

        // Rotate handles at corners (slightly outside)
        var edgeOut = 18.0;
        var rotateSize = 14.0;

        // Top-left
        Canvas.SetLeft(RotateTopLeft, -edgeOut);
        Canvas.SetTop(RotateTopLeft, -edgeOut);

        // Top-right
        Canvas.SetLeft(RotateTopRight, SelectionGroup.Width - rotateSize + edgeOut);
        Canvas.SetTop(RotateTopRight, -edgeOut);

        // Bottom-right
        Canvas.SetLeft(RotateBottomRight, SelectionGroup.Width - rotateSize + edgeOut);
        Canvas.SetTop(RotateBottomRight, SelectionGroup.Height - rotateSize + edgeOut);

        // Bottom-left
        Canvas.SetLeft(RotateBottomLeft, -edgeOut);
        Canvas.SetTop(RotateBottomLeft, SelectionGroup.Height - rotateSize + edgeOut);
    }

    private void ShowRotateHandles(bool show)
    {
        if (!show)
        {
            if (RotateTopLeft != null) RotateTopLeft.IsVisible = false;
            if (RotateTopRight != null) RotateTopRight.IsVisible = false;
            if (RotateBottomLeft != null) RotateBottomLeft.IsVisible = false;
            if (RotateBottomRight != null) RotateBottomRight.IsVisible = false;
        }
        else
        {
            // Default to all hidden; caller should use ShowOnlyNearestCornerRotateHandle
            if (RotateTopLeft != null) RotateTopLeft.IsVisible = false;
            if (RotateTopRight != null) RotateTopRight.IsVisible = false;
            if (RotateBottomLeft != null) RotateBottomLeft.IsVisible = false;
            if (RotateBottomRight != null) RotateBottomRight.IsVisible = false;
        }
    }

    private void ShowOnlyNearestCornerRotateHandle(Point pLocal)
    {
        // Pick nearest corner in unrotated local space
        var w = SelectionGroup.Width;
        var h = SelectionGroup.Height;
        var corners = new (Point pt, Control handle)[]
        {
            (new Point(0,0), RotateTopLeft),
            (new Point(w,0), RotateTopRight),
            (new Point(0,h), RotateBottomLeft),
            (new Point(w,h), RotateBottomRight)
        };

        Control? nearest = null;
        double best = double.MaxValue;
        foreach (var (pt, handle) in corners)
        {
            var dx = pLocal.X - pt.X;
            var dy = pLocal.Y - pt.Y;
            var d2 = dx * dx + dy * dy;
            if (d2 < best)
            {
                best = d2;
                nearest = handle;
            }
        }

        // Only show when within a small radius of the nearest corner
        var maxRadius = 28.0; // tighten to create gaps between targets
        var within = best <= maxRadius * maxRadius;

        if (RotateTopLeft != null) RotateTopLeft.IsVisible = false;
        if (RotateTopRight != null) RotateTopRight.IsVisible = false;
        if (RotateBottomLeft != null) RotateBottomLeft.IsVisible = false;
        if (RotateBottomRight != null) RotateBottomRight.IsVisible = false;

        if (nearest != null && within) nearest.IsVisible = true;
    }

    private static double ComputeAngleDegrees(Point center, Point p)
    {
        var dx = p.X - center.X;
        var dy = p.Y - center.Y;
        return Math.Atan2(dy, dx) * 180.0 / Math.PI;
    }

    private static double NormalizeAngle(double degrees)
    {
        while (degrees <= -180) degrees += 360;
        while (degrees > 180) degrees -= 360;
        return degrees;
    }

    private bool IsPointerNearCornerRotateZoneLocal(Point p)
    {
        // Work in SelectionGroup local coords (before rotation)
        var w = SelectionGroup.Width;
        var h = SelectionGroup.Height;
        var minRadius = 14.0;
        var maxRadius = 36.0;

        bool Near(Point a)
        {
            var dx = p.X - a.X;
            var dy = p.Y - a.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            return d >= minRadius && d <= maxRadius;
        }

        return Near(new Point(0,0)) ||
               Near(new Point(w,0)) ||
               Near(new Point(0,h)) ||
               Near(new Point(w,h));
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        // Reset to initial state
        _originalBitmap?.Dispose();
        _originalBitmap = null;
        
        MainImage.Source = null;
        MainImage.IsVisible = false;
        PreviewImage.Source = null;
        PreviewImage.IsVisible = false;
        BackgroundPattern.IsVisible = true;
        CropOverlay.IsVisible = false;
        BackButton.Opacity = 0;
        BackButton.IsHitTestVisible = false;
        
        ResetButton.IsEnabled = false;
        SaveButton.IsEnabled = false;

        // Clear fixed sizing so layout returns to default when no image
        MainImageArea.ClearValue(WidthProperty);
        MainImageArea.ClearValue(HeightProperty);
        MainImageArea.ClearValue(HorizontalAlignmentProperty);
        MainImageArea.ClearValue(VerticalAlignmentProperty);
        if (ContentGrid.ColumnDefinitions.Count >= 1)
        {
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        }
    }

    private void ResetButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_originalBitmap != null)
        {
            UpdateCropSelectionSize();
            UpdatePreview();
        }
    }

    private async void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_originalBitmap == null) return;

        string? savePath = null;
        
        // Use SavePathProvider if available, otherwise use file picker
        if (SavePathProvider != null)
        {
            savePath = await SavePathProvider();
        }
        else
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Cropped Image",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG Image")
                    {
                        Patterns = new[] { "*.png" }
                    },
                    new FilePickerFileType("JPEG Image")
                    {
                        Patterns = new[] { "*.jpg" }
                    }
                },
                DefaultExtension = "png",
                SuggestedFileName = "cropped-profile-image"
            });

            if (file != null)
            {
                savePath = file.Path.LocalPath;
            }
        }

        if (!string.IsNullOrEmpty(savePath))
        {
            try
            {
                var croppedBitmap = CropImage(_originalBitmap, _cropRect);
                using var stream = File.Create(savePath);
                croppedBitmap.Save(stream);
                
                // Trigger ImageSaved event
                ImageSaved?.Invoke(this, savePath);
            }
            catch
            {
                // Handle save errors silently
            }
        }
    }
}
