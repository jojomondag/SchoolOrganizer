using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using IOPath = System.IO.Path;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;
using SchoolOrganizer.Views.Windows.ImageCrop;
namespace SchoolOrganizer.Views.Windows;
public partial class ImageCropWindow : Window
{
    #region Constants
    private const double MinimumCropSize = 50;
    private const double MaximumCropSize = 400;
    private const double MaximumCropRatio = 0.8;
    private const double SidebarWidth = 260;
    private const double ScreenMargin = 60;
    private const double WindowChromeHorizontal = 329;
    private const double WindowChromeVertical = 50;
    private const int MinimumWindowWidth = 600;
    private const int MinimumWindowHeight = 520;
    private const int MaximumImageSize = 800;
    private static readonly string[] SupportedImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
    private const string OriginalsDirectory = "Data/ProfileImages/Originals";
    private const string ProfileDirectory = "Data/ProfileImages";
    #endregion
    #region Fields
    private Bitmap? _currentBitmap;
    private bool _isDragging;
    private bool _isResizing;
    private string? _activeHandle;
    private Rect _cropArea;
    private Rect _dragStartCropArea;
    private Size _imageDisplaySize;
    private Point _imageDisplayOffset;
    private Point _pointerStartPosition;
    private double _rotationAngle;
    private double _rotationStartAngle;
    private double _rotationInitialAngle;
    private object? _pendingCropSettings;
    private bool _shouldRestoreSettings;
    private MainImageDisplay? _mainImageDisplay;
    private CropPreview? _cropPreview;
    private ImageHistory? _imageHistory;
    private int? _studentId;
    private string? _currentOriginalImagePath;
    #endregion
    #region Properties
    public Func<Task<string?>>? SavePathProvider { get; set; }
    public Func<Task<string[]>>? AvailableImagesProvider { get; set; }
    public Func<string, Task<object?>>? CropSettingsProvider { get; set; }
    public event EventHandler<string>? ImageSaved;
    public event EventHandler<IStorageFile>? OriginalImageSelected;
    public string? SavedImagePath { get; private set; }
    public string? SavedCropSettings { get; private set; }
    public string? SavedOriginalImagePath { get; private set; }
    public int? StudentId => _studentId;
    private Grid? BackgroundPattern => _mainImageDisplay?.GetBackgroundPattern();
    private Image? MainImage => _mainImageDisplay?.GetMainImage();
    private Grid? CropOverlay => _mainImageDisplay?.GetCropOverlay();
    private AvaloniaPath? OverlayCutout => _mainImageDisplay?.GetOverlayCutout();
    private Grid? SelectionGroup => _mainImageDisplay?.GetSelectionGroup();
    private Border? CropSelection => _mainImageDisplay?.GetCropSelection();
    private Border?[] Handles => new[] { 
        _mainImageDisplay?.GetHandleTopLeft(),
        _mainImageDisplay?.GetHandleTopRight(),
        _mainImageDisplay?.GetHandleBottomLeft(),
        _mainImageDisplay?.GetHandleBottomRight()
    };
    #endregion
    #region Constructor and Initialization
    public ImageCropWindow()
    {
        InitializeComponent();
        InitializeMainImageDisplay();
        InitializeEventHandlers();
        AttachWindowEventHandlers();
    }
    private void InitializeMainImageDisplay()
    {
        _mainImageDisplay = this.FindControl<MainImageDisplay>("MainImageDisplayControl");
        _cropPreview = this.FindControl<CropPreview>("CropPreviewControl");
        _imageHistory = this.FindControl<ImageHistory>("ImageHistoryControl");

        if (_mainImageDisplay != null)
            _mainImageDisplay.ImageAreaClicked += async (s, e) => await OnImageAreaClicked();

        if (_cropPreview != null)
        {
            _cropPreview.BackClicked += (s, e) => ResetImageState();
            _cropPreview.ResetClicked += (s, e) => { if (_currentBitmap != null) { UpdateCropSize(); UpdatePreview(); } };
            _cropPreview.SaveClicked += async (s, e) => await HandleSaveButtonAsync();
        }

        if (_imageHistory != null)
        {
            _imageHistory.ImageSelected += async (s, path) => await OnImageHistorySelected(path);
            _imageHistory.ImageDeleted += async (s, path) => await TryExecute(async () => 
            {
                if (File.Exists(path))
                {
                    var wasLoaded = _currentBitmap != null && MainImage?.IsVisible == true;
                    File.Delete(path);
                    await Task.Delay(100);
                    await LoadGallery();
                    if (wasLoaded) await Dispatcher.UIThread.InvokeAsync(ResetImageState);
                }
            });
        }
    }

    private void AttachWindowEventHandlers()
    {
        KeyDown += (s, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Loaded += async (s, e) => { CenterWindow(); await LoadGallery(); };
        ImageSaved += (s, path) =>
        {
            SavedImagePath = path;
            Close();
        };
        OriginalImageSelected += async (s, file) => await LoadGallery();
        SavePathProvider = PrepareSavePath;
        AvailableImagesProvider = GetAvailableImages;
    }
    #endregion
    #region Public Methods
    public static async Task<string?> ShowAsync(Window parent)
    {
        var dialog = new ImageCropWindow();
        await dialog.ShowDialog(parent);
        return dialog.SavedImagePath;
    }

    public static async Task<(string? imagePath, string? cropSettings, string? originalImagePath)> ShowForStudentAsync(
        Window parent,
        int studentId,
        string? existingOriginalImagePath = null,
        string? cropSettingsJson = null)
    {
        var dialog = new ImageCropWindow();
        dialog._studentId = studentId;

        // Load the ORIGINAL image with crop settings if available
        if (!string.IsNullOrEmpty(existingOriginalImagePath) && File.Exists(existingOriginalImagePath))
        {
            object? settings = null;
            if (!string.IsNullOrEmpty(cropSettingsJson))
            {
                try
                {
                    settings = System.Text.Json.JsonSerializer.Deserialize<object>(cropSettingsJson);
                }
                catch
                {
                    // If deserialization fails, ignore and use no settings
                }
            }

            await dialog.LoadImageFromPathWithCropSettingsAsync(existingOriginalImagePath, settings);
        }

        await dialog.ShowDialog(parent);
        return (dialog.SavedImagePath, dialog.SavedCropSettings, dialog.SavedOriginalImagePath);
    }
    public async Task LoadImageFromPathAsync(string path)
    {
        await LoadImageFromPathWithCropSettingsAsync(path, null);
    }
    public async Task LoadImageFromPathWithCropSettingsAsync(string path, object? settings)
    {
        if (!File.Exists(path)) return;

        // Track the original image path
        _currentOriginalImagePath = path;

        await using var fileStream = File.OpenRead(path);
        var bitmap = new Bitmap(fileStream);
        var resized = ResizeImageIfNeeded(bitmap);
        if (resized != bitmap)
        {
            bitmap.Dispose();
        }
        await LoadBitmap(resized, settings);
    }
    public async Task RefreshImageGalleryAsync()
    {
        await LoadGallery();
    }
    public object? GetCurrentCropSettings()
    {
        if (!IsCropStateValid())
        {
            return null;
        }
        return new
        {
            X = _cropArea.X,
            Y = _cropArea.Y,
            Width = _cropArea.Width,
            Height = _cropArea.Height,
            RotationAngle = _rotationAngle,
            ImageDisplayWidth = _imageDisplaySize.Width,
            ImageDisplayHeight = _imageDisplaySize.Height,
            ImageDisplayOffsetX = _imageDisplayOffset.X,
            ImageDisplayOffsetY = _imageDisplayOffset.Y
        };
    }
    #endregion
    #region Window Layout
    private void CenterWindow()
    {
        if (Screens?.Primary is { } screen)
        {
            var workingArea = screen.WorkingArea;
            var centerX = (int)((workingArea.Width - Width) / 2 + workingArea.X);
            var centerY = (int)((workingArea.Height - Height) / 2 + workingArea.Y);
            Position = new PixelPoint(centerX, centerY);
        }
    }
    private void AdjustWindowSize()
    {
        if (_currentBitmap == null || GetTopLevel(this) is not Window window || window.Screens?.Primary is not { } screen)
        {
            return;
        }
        var workingArea = screen.WorkingArea;
        var maxWidth = workingArea.Width - ScreenMargin;
        var maxHeight = workingArea.Height - ScreenMargin;
        var maxContentWidth = maxWidth - WindowChromeHorizontal;
        var maxContentHeight = maxHeight - WindowChromeVertical;
        var contentBounds = new Rect(0, 0,
            Math.Min(maxContentWidth, _currentBitmap.PixelSize.Width),
            Math.Min(maxContentHeight, _currentBitmap.PixelSize.Height));
        var (targetSize, _) = CalculateFitSize(contentBounds, _currentBitmap.PixelSize);
        var windowWidth = Math.Max(targetSize.Width + WindowChromeHorizontal, MinimumWindowWidth);
        var windowHeight = Math.Max(targetSize.Height + WindowChromeVertical, MinimumWindowHeight);
        if (windowWidth > maxWidth || windowHeight > maxHeight)
        {
            windowWidth = Math.Min(windowWidth, maxWidth);
            windowHeight = Math.Min(windowHeight, maxHeight);
        }
        window.Width = windowWidth;
        window.Height = windowHeight;
        window.Position = new PixelPoint(
            workingArea.X + (int)((workingArea.Width - window.Width) / 2),
            workingArea.Y + (int)((workingArea.Height - window.Height) / 2)
        );
    }
    #endregion
    #region Image Loading
    private async Task OnImageAreaClicked()
    {
        if (_currentBitmap == null)
        {
            await SelectImage();
        }
    }
    private async Task SelectImage()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }
        var filePickerOptions = new FilePickerOpenOptions
        {
            Title = "Select Image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Image Files")
                {
                    Patterns = SupportedImageExtensions.Select(ext => $"*{ext}").ToArray()
                }
            ]
        };
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(filePickerOptions);
        if (files.Count > 0)
        {
            OriginalImageSelected?.Invoke(this, files[0]);
            using var stream = await files[0].OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            var bitmap = new Bitmap(memoryStream);
            memoryStream.Position = 0;
            _currentOriginalImagePath = await SaveToOriginals(bitmap, files[0].Name);
            var resized = ResizeImageIfNeeded(bitmap);
            if (resized != bitmap)
            {
                bitmap.Dispose();
            }
            await LoadBitmap(resized);
        }
    }
    private async Task<string?> SaveToOriginals(Bitmap bitmap, string filename)
    {
        try
        {
            var directory = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, OriginalsDirectory);
            Directory.CreateDirectory(directory);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var fullPath = IOPath.Combine(directory, $"{timestamp}_{filename}");
            await using var stream = File.Create(fullPath);
            bitmap.Save(stream);
            return fullPath;
        }
        catch
        {
            return null;
        }
    }
    private Bitmap ResizeImageIfNeeded(Bitmap original)
    {
        var width = original.PixelSize.Width;
        var height = original.PixelSize.Height;
        if (width <= MaximumImageSize && height <= MaximumImageSize)
        {
            return original;
        }
        double scale = width > height
            ? (double)MaximumImageSize / width
            : (double)MaximumImageSize / height;
        var newWidth = (int)(width * scale);
        var newHeight = (int)(height * scale);
        var resized = new RenderTargetBitmap(new PixelSize(newWidth, newHeight));
        using var drawingContext = resized.CreateDrawingContext();
        drawingContext.DrawImage(
            original,
            new Rect(0, 0, width, height),
            new Rect(0, 0, newWidth, newHeight)
        );
        return resized;
    }
    private async Task LoadBitmap(Bitmap bitmap, object? settings = null)
    {
        _currentBitmap?.Dispose();
        _currentBitmap = bitmap;
        if (MainImage == null || BackgroundPattern == null || CropOverlay == null)
        {
            return;
        }
        MainImage.Source = _currentBitmap;
        MainImage.IsVisible = true;
        BackgroundPattern.IsVisible = false;
        CropOverlay.IsVisible = true;
        SetPreviewState(true, true, true);
        AdjustWindowSize();
        CenterWindow();
        this.CanResize = false;
        await Task.Delay(200);
        if (settings != null && TryRestoreSettings(settings))
        {
            ApplyCropTransform();
            UpdatePreview();
        }
        else if (settings != null)
        {
            _pendingCropSettings = settings;
            _shouldRestoreSettings = true;
            UpdateCropSize();
        }
        else
        {
            UpdateCropSize();
        }
        await LoadGallery();
        if (IsCropStateValid())
        {
            UpdatePreview();
        }
    }
    private async Task OnImageHistorySelected(string path)
    {
        var settings = await TryExecute(async () => 
            CropSettingsProvider != null ? await CropSettingsProvider(path) : null);
        await LoadImageFromPathWithCropSettingsAsync(path, settings);
    }
    private async Task LoadGallery() => await TryExecute(async () =>
    {
        if (AvailableImagesProvider != null && _imageHistory != null)
        {
            var imagePaths = await AvailableImagesProvider();
            await _imageHistory.LoadGalleryAsync(imagePaths, CropSettingsProvider);
        }
    });
    #endregion
    #region Crop Area Management
    private void UpdateCropSize()
    {
        if (_currentBitmap == null || CropOverlay == null)
        {
            return;
        }
        var bounds = CropOverlay.Bounds;
        if (bounds.Width == 0 || bounds.Height == 0)
        {
            SetDefaultCropValues();
            return;
        }
        CalculateDisplayMetrics(bounds);
        InitializeCrop();
    }
    private void SetDefaultCropValues()
    {
        _cropArea = new Rect(100, 50, 200, 200);
        _imageDisplaySize = new Size(400, 300);
        _imageDisplayOffset = new Point(100, 50);
    }
    private void CalculateDisplayMetrics(Rect containerBounds)
    {
        var (displaySize, offset) = CalculateFitSize(containerBounds, _currentBitmap!.PixelSize);
        _imageDisplaySize = displaySize;
        _imageDisplayOffset = offset;
    }
    private (Size displaySize, Point offset) CalculateFitSize(Rect containerBounds, PixelSize imageSize)
    {
        var imageAspectRatio = (double)imageSize.Width / imageSize.Height;
        var containerAspectRatio = containerBounds.Width / containerBounds.Height;
        if (imageAspectRatio > containerAspectRatio)
        {
            var displaySize = new Size(containerBounds.Width, containerBounds.Width / imageAspectRatio);
            var marginY = (containerBounds.Height - displaySize.Height) / 2;
            return (displaySize, new Point(0, Math.Max(0, marginY)));
        }
        else
        {
            var displaySize = new Size(containerBounds.Height * imageAspectRatio, containerBounds.Height);
            var marginX = (containerBounds.Width - displaySize.Width) / 2;
            return (displaySize, new Point(Math.Max(0, marginX), 0));
        }
    }
    private void InitializeCrop()
    {
        if (CropSelection == null)
        {
            return;
        }
        var size = Math.Clamp(
            Math.Min(_imageDisplaySize.Width, _imageDisplaySize.Height) * 0.5,
            MinimumCropSize,
            CalculateMaximumCropSize()
        );
        CropSelection.Width = size;
        CropSelection.Height = size;
        _cropArea = new Rect(
            _imageDisplayOffset.X + (_imageDisplaySize.Width - size) / 2,
            _imageDisplayOffset.Y + (_imageDisplaySize.Height - size) / 2,
            size,
            size
        );
        _rotationAngle = 0;
        ApplyCropTransform();
    }
    private void ApplyCropTransform()
    {
        if (SelectionGroup == null || CropSelection == null)
        {
            return;
        }
        var snappedRect = SnapToPixels(_cropArea);
        SelectionGroup.Width = snappedRect.Width;
        SelectionGroup.Height = snappedRect.Height;
        SelectionGroup.Margin = new Thickness(snappedRect.X, snappedRect.Y, 0, 0);
        CropSelection.Width = snappedRect.Width;
        CropSelection.Height = snappedRect.Height;
        CropSelection.CornerRadius = new CornerRadius(snappedRect.Width / 2);
        UpdateRotation();
        UpdateCutout();
    }
    private void UpdateCutout()
    {
        if (CropOverlay == null || CropOverlay.Bounds.Width == 0 || OverlayCutout == null)
        {
            return;
        }
        var outerRect = SnapToPixels(new Rect(0, 0, CropOverlay.Bounds.Width, CropOverlay.Bounds.Height));
        var innerRect = SnapToPixels(_cropArea);
        var center = innerRect.Center;
        var radius = innerRect.Width / 2;
        OverlayCutout.Data = new GeometryGroup
        {
            FillRule = FillRule.EvenOdd,
            Children =
            {
                new RectangleGeometry(outerRect),
                new EllipseGeometry
                {
                    Center = center,
                    RadiusX = radius,
                    RadiusY = radius
                }
            }
        };
    }
    private void UpdateRotation()
    {
        if (SelectionGroup == null)
        {
            return;
        }
        SelectionGroup.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        SelectionGroup.RenderTransform = new RotateTransform(_rotationAngle);
        PositionHandles();
    }
    private void PositionHandles()
    {
        if (SelectionGroup == null)
        {
            return;
        }
        var width = SelectionGroup.Width;
        var center = width / 2;
        var radius = width / 2;
        var angles = new[] { 45.0, 135.0, 225.0, 315.0 };
        for (int i = 0; i < 4; i++)
        {
            var handle = Handles[i];
            if (handle == null) continue;
            var angleInRadians = angles[i] * Math.PI / 180;
            var x = center + radius * Math.Cos(angleInRadians) - 7;
            var y = center + radius * Math.Sin(angleInRadians) - 7;
            Canvas.SetLeft(handle, x);
            Canvas.SetTop(handle, y);
        }
    }
    private double CalculateMaximumCropSize()
    {
        if (_imageDisplaySize.Width <= 0 || _imageDisplaySize.Height <= 0)
        {
            return MaximumCropSize;
        }
        var maxFromImage = Math.Min(_imageDisplaySize.Width, _imageDisplaySize.Height) * MaximumCropRatio;
        var maxFromWindow = Math.Min(Width - SidebarWidth - 60, Height - 80) * 0.6;
        return Math.Min(MaximumCropSize, Math.Min(maxFromImage, maxFromWindow));
    }
    private bool TryRestoreSettings(object? settings)
    {
        if (settings == null || _currentBitmap == null)
        {
            return false;
        }
        try
        {
            var type = settings.GetType();
            var properties = new[]
            {
                "X", "Y", "Width", "Height",
                "ImageDisplayWidth", "ImageDisplayHeight",
                "ImageDisplayOffsetX", "ImageDisplayOffsetY"
            }.Select(name => type.GetProperty(name)).ToArray();
            if (properties.Any(p => p == null))
            {
                return false;
            }
            var savedX = Convert.ToDouble(properties[0]!.GetValue(settings));
            var savedY = Convert.ToDouble(properties[1]!.GetValue(settings));
            var savedWidth = Convert.ToDouble(properties[2]!.GetValue(settings));
            var savedHeight = Convert.ToDouble(properties[3]!.GetValue(settings));
            var savedDisplayWidth = Convert.ToDouble(properties[4]!.GetValue(settings));
            var savedDisplayHeight = Convert.ToDouble(properties[5]!.GetValue(settings));
            var savedOffsetX = Convert.ToDouble(properties[6]!.GetValue(settings));
            var savedOffsetY = Convert.ToDouble(properties[7]!.GetValue(settings));
            var rotationProperty = type.GetProperty("RotationAngle");
            var savedRotation = rotationProperty != null
                ? Convert.ToDouble(rotationProperty.GetValue(settings))
                : 0;
            if (CropOverlay == null)
            {
                return false;
            }
            var bounds = CropOverlay.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return false;
            }
            var (currentDisplaySize, currentOffset) = CalculateFitSize(bounds, _currentBitmap.PixelSize);
            var scaleX = savedDisplayWidth > 0 ? currentDisplaySize.Width / savedDisplayWidth : 1;
            var scaleY = savedDisplayHeight > 0 ? currentDisplaySize.Height / savedDisplayHeight : 1;
            var relativeX = savedX - savedOffsetX;
            var relativeY = savedY - savedOffsetY;
            var newX = currentOffset.X + relativeX * scaleX;
            var newY = currentOffset.Y + relativeY * scaleY;
            var newWidth = savedWidth * scaleX;
            var newHeight = savedHeight * scaleY;
            newWidth = Math.Max(1, Math.Min(newWidth, currentDisplaySize.Width));
            newHeight = Math.Max(1, Math.Min(newHeight, currentDisplaySize.Height));
            newX = Math.Max(currentOffset.X, Math.Min(currentOffset.X + currentDisplaySize.Width - newWidth, newX));
            newY = Math.Max(currentOffset.Y, Math.Min(currentOffset.Y + currentDisplaySize.Height - newHeight, newY));
            _imageDisplaySize = currentDisplaySize;
            _imageDisplayOffset = currentOffset;
            _cropArea = new Rect(newX, newY, newWidth, newHeight);
            _rotationAngle = savedRotation;
            return true;
        }
        catch
        {
            return false;
        }
    }
    #endregion
    #region Interaction Event Handlers
    private void InitializeEventHandlers()
    {
        if (CropSelection == null || SelectionGroup == null || CropOverlay == null)
        {
            return;
        }
        CropSelection.PointerPressed += OnCropSelectionPressed;
        CropSelection.PointerMoved += OnCropSelectionMoved;
        CropSelection.PointerReleased += OnCropSelectionReleased;
        CropSelection.PointerCaptureLost += (s, e) => _isResizing = false;
        CropSelection.PointerExited += (s, e) =>
        {
            if (!_isResizing && CropSelection != null)
            {
                CropSelection.Cursor = new Cursor(StandardCursorType.SizeAll);
            }
        };
        SelectionGroup.PointerPressed += OnSelectionGroupPressed;
        SelectionGroup.PointerMoved += OnSelectionGroupMoved;
        SelectionGroup.PointerReleased += OnSelectionGroupReleased;
        SelectionGroup.PointerCaptureLost += (s, e) => _isDragging = false;
        CropOverlay.PointerPressed += OnOverlayPressed;
        CropOverlay.PointerMoved += OnOverlayMoved;
        CropOverlay.PointerReleased += OnOverlayReleased;
        CropOverlay.PointerCaptureLost += (s, e) =>
        {
            _isDragging = false;
            _isResizing = false;
            _activeHandle = null;
        };
        CropOverlay.SizeChanged += OnOverlaySizeChanged;
        AttachHandleEvents();
    }
    private void OnOverlaySizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_currentBitmap != null && CropOverlay?.IsVisible == true)
        {
            UpdateCropSize();
            if (_shouldRestoreSettings && _pendingCropSettings != null && TryRestoreSettings(_pendingCropSettings))
            {
                _shouldRestoreSettings = false;
                _pendingCropSettings = null;
                ApplyCropTransform();
                UpdatePreview();
            }
        }
    }
    private void AttachHandleEvents()
    {
        var handlePositions = new[] { "tl", "tr", "bl", "br" };
        for (int i = 0; i < 4; i++)
        {
            var position = handlePositions[i];
            var handle = Handles[i];
            if (handle == null || CropOverlay == null) continue;

            handle.PointerPressed += (s, e) =>
            {
                if (CropOverlay != null && IsLeftButtonPressed(e, CropOverlay))
                {
                    _isResizing = true;
                    _activeHandle = position;
                    _pointerStartPosition = e.GetPosition(CropOverlay);
                    _rotationStartAngle = CalculateAngle(_cropArea.Center, _pointerStartPosition);
                    _rotationInitialAngle = _rotationAngle;
                    e.Pointer.Capture(CropOverlay);
                    e.Handled = true;
                }
            };
            handle.PointerReleased += (s, e) => { EndDragAndResize(); e.Pointer.Capture(null); e.Handled = true; };
        }
    }
    private void OnCropSelectionPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!ValidateControls(CropSelection, CropOverlay))
            return;
        if (!IsLeftButtonPressed(e, CropSelection!))
            return;

        var position = e.GetPosition(CropSelection);
        var center = new Point(CropSelection!.Width / 2, CropSelection.Height / 2);
        var (distance, deltaX, deltaY) = CalculateDistanceAndDeltas(position, center);
        var radius = CropSelection.Width / 2;

        if (IsOnResizeEdge(distance, radius))
        {
            StartResize(e, CropOverlay!, CropSelection, deltaX, deltaY);
        }
    }

    private void OnCropSelectionMoved(object? sender, PointerEventArgs e)
    {
        if (!ValidateControls(CropSelection, CropOverlay))
            return;

        if (!_isResizing)
        {
            UpdateCursorBasedOnPosition(e.GetPosition(CropSelection!), CropSelection!);
            return;
        }

        if (!ContinueDragOperation(e, CropOverlay!))
        {
            EndResize();
            return;
        }

        PerformResize(e.GetPosition(CropOverlay!));
        ApplyCropTransform();
        UpdatePreview();
        e.Handled = true;
    }

    private void OnCropSelectionReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (CropSelection != null)
        {
            EndResize();
            CropSelection.Cursor = new Cursor(StandardCursorType.SizeAll);
        }
        e.Pointer.Capture(null);
        e.Handled = true;
    }
    private void OnSelectionGroupPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!ValidateControls(CropSelection, CropOverlay))
            return;
        if (!IsLeftButtonPressed(e, CropSelection!))
            return;

        StartDrag(e, CropOverlay!, CropSelection!);
    }

    private void OnSelectionGroupMoved(object? sender, PointerEventArgs e)
    {
        if (!ValidateControls(SelectionGroup, CropOverlay))
            return;

        if (!_isDragging)
        {
            SelectionGroup!.Cursor = new Cursor(StandardCursorType.SizeAll);
            return;
        }

        if (!ContinueDragOperation(e, CropOverlay!))
        {
            EndDrag(SelectionGroup!);
            return;
        }

        PerformDrag(e.GetPosition(CropOverlay!));
        e.Handled = true;
    }

    private void OnSelectionGroupReleased(object? sender, PointerReleasedEventArgs e)
    {
        EndDragAndResize();
        e.Pointer.Capture(null);
        e.Handled = true;
    }
    private void OnOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        if (CropOverlay == null || !IsLeftButtonPressed(e, CropOverlay))
            return;

        var position = e.GetPosition(CropOverlay);
        var center = new Point(_cropArea.X + _cropArea.Width / 2, _cropArea.Y + _cropArea.Height / 2);
        var (distance, deltaX, deltaY) = CalculateDistanceAndDeltas(position, center);

        if (IsOnResizeEdge(distance, _cropArea.Width / 2))
        {
            StartResize(e, CropOverlay, CropOverlay, deltaX, deltaY);
        }
    }

    private void OnOverlayMoved(object? sender, PointerEventArgs e)
    {
        if (CropOverlay == null)
            return;

        if (_isResizing && !string.IsNullOrEmpty(_activeHandle))
        {
            if (!ContinueDragOperation(e, CropOverlay))
            {
                EndDragAndResize();
                e.Handled = true;
                return;
            }
            PerformRotation(e.GetPosition(CropOverlay));
            e.Handled = true;
        }
        else if (_isResizing)
        {
            if (!ContinueDragOperation(e, CropOverlay))
            {
                EndResize();
                return;
            }
            PerformResize(e.GetPosition(CropOverlay));
            ApplyCropTransform();
            UpdateCutout();
            UpdatePreview();
            e.Handled = true;
        }
        else if (!_isDragging && string.IsNullOrEmpty(_activeHandle))
        {
            UpdateCursorBasedOnCropArea(e.GetPosition(CropOverlay), CropOverlay);
        }
    }

    private void OnOverlayReleased(object? sender, PointerReleasedEventArgs e)
    {
        EndDragAndResize();
        e.Pointer.Capture(null);
        e.Handled = true;
    }
    #endregion
    #region Image Processing
    private void UpdatePreview() => 
        _cropPreview?.UpdatePreview(IsCropStateValid() ? CreateCroppedImage() : null);
    private Bitmap? CreateCroppedImage()
    {
        if (!IsCropStateValid())
        {
            return null;
        }
        var scaleX = _currentBitmap!.PixelSize.Width / _imageDisplaySize.Width;
        var scaleY = _currentBitmap.PixelSize.Height / _imageDisplaySize.Height;
        var cropX = (_cropArea.X - _imageDisplayOffset.X) * scaleX;
        var cropY = (_cropArea.Y - _imageDisplayOffset.Y) * scaleY;
        var cropWidth = _cropArea.Width * scaleX;
        var cropHeight = _cropArea.Height * scaleY;
        var outputSize = (int)Math.Ceiling(Math.Max(cropWidth, cropHeight));
        var renderTarget = new RenderTargetBitmap(new PixelSize(outputSize, outputSize));
        using var drawingContext = renderTarget.CreateDrawingContext();
        var centerX = cropX + cropWidth / 2.0;
        var centerY = cropY + cropHeight / 2.0;
        var clipRect = new Rect(0, 0, outputSize, outputSize);
        using (drawingContext.PushClip(clipRect))
        using (drawingContext.PushGeometryClip(new EllipseGeometry(clipRect)))
        {
            if (Math.Abs(_rotationAngle) <= 0.01)
            {
                var sourceRect = new Rect(
                    centerX - outputSize / 2.0,
                    centerY - outputSize / 2.0,
                    outputSize,
                    outputSize
                );
                var destRect = new Rect(0, 0, outputSize, outputSize);
                drawingContext.DrawImage(_currentBitmap, sourceRect, destRect);
            }
            else
            {
                using (drawingContext.PushTransform(
                    Matrix.CreateTranslation(-centerX, -centerY) *
                    Matrix.CreateRotation(_rotationAngle * Math.PI / 180) *
                    Matrix.CreateTranslation(outputSize / 2.0, outputSize / 2.0)))
                {
                    drawingContext.DrawImage(
                        _currentBitmap,
                        new Rect(0, 0, _currentBitmap.PixelSize.Width, _currentBitmap.PixelSize.Height),
                        new Rect(0, 0, _currentBitmap.PixelSize.Width, _currentBitmap.PixelSize.Height)
                    );
                }
            }
        }
        return renderTarget;
    }
    #endregion
    #region Button Handlers
    private void ResetImageState()
    {
        _currentBitmap?.Dispose();
        _currentBitmap = null;
        if (!ValidateControls(MainImage, CropOverlay, BackgroundPattern)) return;
        
        MainImage!.Source = null;
        MainImage.IsVisible = false;
        CropOverlay!.IsVisible = false;
        BackgroundPattern!.IsVisible = true;
        _cropPreview?.UpdatePreview(null);
        SetPreviewState(false, false, false);
    }
    private async Task HandleSaveButtonAsync()
    {
        if (_currentBitmap == null) return;

        var savePath = SavePathProvider != null ? await SavePathProvider() : await PromptForSavePath();
        if (string.IsNullOrEmpty(savePath)) return;

        TryExecute(() =>
        {
            using var stream = File.Create(savePath);
            CreateCroppedImage()?.Save(stream);

            // Save crop settings as JSON
            var settings = GetCurrentCropSettings();
            if (settings != null)
            {
                SavedCropSettings = System.Text.Json.JsonSerializer.Serialize(settings);
            }

            // Save the original image path so we can reload it with crop settings later
            SavedOriginalImagePath = _currentOriginalImagePath;

            ImageSaved?.Invoke(this, savePath);
        });
    }
    private async Task<string?> PromptForSavePath()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null)
        {
            return null;
        }
        var saveOptions = new FilePickerSaveOptions
        {
            Title = "Save Cropped Image",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } },
                new FilePickerFileType("JPEG Image") { Patterns = new[] { "*.jpg" } }
            },
            DefaultExtension = "png",
            SuggestedFileName = "cropped-profile-image"
        };
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(saveOptions);
        return file?.Path.LocalPath;
    }
    #endregion
    #region Helper Methods for Interaction
    private static bool ValidateControls(params Control?[] controls) => controls.All(c => c != null);

    private static bool IsLeftButtonPressed(PointerPressedEventArgs e, Control control) =>
        e.GetCurrentPoint(control).Properties.IsLeftButtonPressed;

    private bool ContinueDragOperation(PointerEventArgs e, Control control) =>
        e.GetCurrentPoint(control).Properties.IsLeftButtonPressed;

    private static (double distance, double deltaX, double deltaY) CalculateDistanceAndDeltas(Point position, Point center)
    {
        var deltaX = position.X - center.X;
        var deltaY = position.Y - center.Y;
        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        return (distance, deltaX, deltaY);
    }

    private static bool IsOnResizeEdge(double distance, double radius) =>
        distance > radius - 20 && distance < radius + 10;

    private void StartResize(PointerPressedEventArgs e, Control overlay, Control cursor, double deltaX, double deltaY)
    {
        _isResizing = true;
        _pointerStartPosition = e.GetPosition(overlay);
        _dragStartCropArea = _cropArea;
        cursor.Cursor = new Cursor(GetResizeCursor(deltaX, deltaY));
        e.Pointer.Capture(cursor);
        e.Handled = true;
    }

    private void StartDrag(PointerPressedEventArgs e, Control overlay, Control cursor)
    {
        _isDragging = true;
        _pointerStartPosition = e.GetPosition(overlay);
        _dragStartCropArea = _cropArea;
        cursor.Cursor = new Cursor(StandardCursorType.SizeAll);
        e.Pointer.Capture(cursor);
        e.Handled = true;
    }

    private void EndResize()
    {
        _isResizing = false;
    }

    private void EndDrag(Control? control)
    {
        _isDragging = false;
        if (control != null)
            control.Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    private void EndDragAndResize()
    {
        _isDragging = false;
        _isResizing = false;
        _activeHandle = null;
    }

    private void UpdateCursorBasedOnPosition(Point position, Control control)
    {
        var center = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var radius = control.Bounds.Width / 2;
        UpdateCursorForPosition(position, center.X, center.Y, radius, control);
    }

    private void UpdateCursorBasedOnCropArea(Point position, Control control)
    {
        var centerX = _cropArea.X + _cropArea.Width / 2;
        var centerY = _cropArea.Y + _cropArea.Height / 2;
        var radius = _cropArea.Width / 2;
        UpdateCursorForPosition(position, centerX, centerY, radius, control);
    }

    private void UpdateCursorForPosition(Point position, double centerX, double centerY, double radius, Control control)
    {
        var (distance, deltaX, deltaY) = CalculateDistanceAndDeltas(position, new Point(centerX, centerY));
        
        if (IsOnResizeEdge(distance, radius))
            control.Cursor = new Cursor(GetResizeCursor(deltaX, deltaY));
        else if (distance <= radius - 20)
            control.Cursor = new Cursor(StandardCursorType.SizeAll);
        else
            control.Cursor = Cursor.Default;
    }

    private void PerformDrag(Point currentPosition)
    {
        var delta = currentPosition - _pointerStartPosition;
        var newX = Math.Clamp(
            _dragStartCropArea.X + delta.X,
            _imageDisplayOffset.X,
            _imageDisplayOffset.X + _imageDisplaySize.Width - _dragStartCropArea.Width
        );
        var newY = Math.Clamp(
            _dragStartCropArea.Y + delta.Y,
            _imageDisplayOffset.Y,
            _imageDisplayOffset.Y + _imageDisplaySize.Height - _dragStartCropArea.Height
        );
        _cropArea = new Rect(newX, newY, _dragStartCropArea.Width, _dragStartCropArea.Height);
        ApplyCropTransform();
        UpdatePreview();
    }

    private void PerformRotation(Point currentPosition)
    {
        var currentAngle = CalculateAngle(_cropArea.Center, currentPosition);
        _rotationAngle = NormalizeAngle(_rotationInitialAngle + (currentAngle - _rotationStartAngle));
        UpdateRotation();
        UpdateCutout();
        UpdatePreview();
    }

    private void PerformResize(Point currentPosition)
    {
        var centerX = _dragStartCropArea.X + _dragStartCropArea.Width / 2;
        var centerY = _dragStartCropArea.Y + _dragStartCropArea.Height / 2;
        var startLength = Math.Sqrt(
            Math.Pow(_pointerStartPosition.X - centerX, 2) +
            Math.Pow(_pointerStartPosition.Y - centerY, 2)
        );
        var currentLength = Math.Sqrt(
            Math.Pow(currentPosition.X - centerX, 2) +
            Math.Pow(currentPosition.Y - centerY, 2)
        );
        if (startLength < 1)
        {
            startLength = 1;
        }
        var halfNewSize = Math.Clamp(
            _dragStartCropArea.Width / 2 * (currentLength / startLength),
            MinimumCropSize / 2,
            CalculateMaximumCropSize() / 2
        );
        halfNewSize = Math.Min(halfNewSize, Math.Min(centerX - _imageDisplayOffset.X, _imageDisplayOffset.X + _imageDisplaySize.Width - centerX));
        halfNewSize = Math.Min(halfNewSize, Math.Min(centerY - _imageDisplayOffset.Y, _imageDisplayOffset.Y + _imageDisplaySize.Height - centerY));
        halfNewSize = Math.Min(halfNewSize, Math.Min(_imageDisplaySize.Width, _imageDisplaySize.Height) * 0.4);
        _cropArea = new Rect(centerX - halfNewSize, centerY - halfNewSize, halfNewSize * 2, halfNewSize * 2);
    }

    private void SetPreviewState(bool showActions, bool resetEnabled, bool saveEnabled)
    {
        _cropPreview?.ShowActions(showActions);
        _cropPreview?.SetResetEnabled(resetEnabled);
        _cropPreview?.SetSaveEnabled(saveEnabled);
    }

    private static async Task TryExecute(Func<Task> action)
    {
        try { await action(); }
        catch { }
    }

    private static async Task<T?> TryExecute<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch { return default; }
    }

    private static void TryExecute(Action action)
    {
        try { action(); }
        catch { }
    }
    #region Utility Methods
    private Task<string?> PrepareSavePath()
    {
        try
        {
            var directory = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, ProfileDirectory);
            Directory.CreateDirectory(directory);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var filename = _studentId.HasValue
                ? $"student_{_studentId.Value}_{timestamp}.png"
                : $"profile_{timestamp}.png";
            return Task.FromResult<string?>(IOPath.Combine(directory, filename));
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }
    private Task<string[]> GetAvailableImages()
    {
        try
        {
            var directory = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, OriginalsDirectory);
            if (!Directory.Exists(directory))
            {
                return Task.FromResult(Array.Empty<string>());
            }
            var imageFiles = Directory.GetFiles(directory)
                .Where(IsValidImageFile)
                .OrderByDescending(File.GetLastWriteTime)
                .ToArray();
            return Task.FromResult(imageFiles);
        }
        catch
        {
            return Task.FromResult(Array.Empty<string>());
        }
    }
    private static bool IsValidImageFile(string filePath)
    {
        var extension = IOPath.GetExtension(filePath).ToLowerInvariant();
        return SupportedImageExtensions.Contains(extension) && File.Exists(filePath);
    }
    private bool IsCropStateValid()
    {
        return _currentBitmap != null
            && _cropArea.Width > 0
            && _cropArea.Height > 0
            && _imageDisplaySize.Width > 0
            && _imageDisplaySize.Height > 0;
    }
    private static double CalculateAngle(Point center, Point point)
    {
        return Math.Atan2(point.Y - center.Y, point.X - center.X) * 180 / Math.PI;
    }
    private static double NormalizeAngle(double angle)
    {
        while (angle <= -180)
        {
            angle += 360;
        }
        while (angle > 180)
        {
            angle -= 360;
        }
        return angle;
    }
    private static Rect SnapToPixels(Rect rect)
    {
        var left = Math.Floor(rect.X);
        var top = Math.Floor(rect.Y);
        var right = Math.Ceiling(rect.Right);
        var bottom = Math.Ceiling(rect.Bottom);
        return new Rect(
            left,
            top,
            Math.Max(0, right - left),
            Math.Max(0, bottom - top)
        );
    }
    private static StandardCursorType GetResizeCursor(double deltaX, double deltaY)
    {
        var angle = Math.Atan2(deltaY, deltaX) * 180 / Math.PI;
        if (angle < 0)
        {
            angle += 360;
        }
        bool isHorizontal = (angle >= 315 || angle < 45) || (angle >= 135 && angle < 225);
        return isHorizontal ? StandardCursorType.SizeWestEast : StandardCursorType.SizeNorthSouth;
    }
    #endregion
    #endregion
}