using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using IOPath = System.IO.Path;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Serilog;

namespace SchoolOrganizer.Src.Views.Windows.ImageCrop;

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
    private const int MaximumImageSize = 4096; // Allow much larger images to preserve quality
    private const int ProfileImageOutputSize = 512; // High-quality output size for profile images
    private static readonly string[] SupportedImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
    private const string OriginalsDirectory = "Data/ProfileImages/Originals";
    private const string ProfileDirectory = "Data/ProfileImages";
    #endregion
    #region Fields
    private Bitmap? _currentBitmap;
    private Bitmap? _fullResolutionBitmap; // Keep full resolution version for high-quality final crop
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
    private bool _isPreviewUpdatePending;
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
            _cropPreview.RotateLeftClicked += (s, e) => RotateImage(-90);
            _cropPreview.RotateRightClicked += (s, e) => RotateImage(90);
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
                    using var doc = JsonDocument.Parse(cropSettingsJson);
                    var root = doc.RootElement;
                    settings = new
                    {
                        X = root.GetProperty("X").GetDouble(),
                        Y = root.GetProperty("Y").GetDouble(),
                        Width = root.GetProperty("Width").GetDouble(),
                        Height = root.GetProperty("Height").GetDouble(),
                        RotationAngle = root.TryGetProperty("RotationAngle", out var rot) ? rot.GetDouble() : 0,
                        ImageDisplayWidth = root.GetProperty("ImageDisplayWidth").GetDouble(),
                        ImageDisplayHeight = root.GetProperty("ImageDisplayHeight").GetDouble(),
                        ImageDisplayOffsetX = root.GetProperty("ImageDisplayOffsetX").GetDouble(),
                        ImageDisplayOffsetY = root.GetProperty("ImageDisplayOffsetY").GetDouble()
                    };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to deserialize crop settings: {ex.Message}");
                    // settings remains null, will use default crop area
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

        Log.Information("Loading image from path: {Path}", path);
        await using var fileStream = File.OpenRead(path);
        var bitmap = LoadBitmapWithCorrectOrientation(fileStream, path);

        // Keep full resolution bitmap for high-quality final crop
        _fullResolutionBitmap?.Dispose();
        _fullResolutionBitmap = bitmap;

        var resized = ResizeImageIfNeeded(bitmap);
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
            var bitmap = LoadBitmapWithCorrectOrientation(memoryStream, files[0].Path.LocalPath);
            memoryStream.Position = 0;
            _currentOriginalImagePath = await SaveToOriginals(bitmap, files[0].Name);

            // Keep full resolution bitmap for high-quality final crop
            _fullResolutionBitmap?.Dispose();
            _fullResolutionBitmap = bitmap;

            var resized = ResizeImageIfNeeded(bitmap);
            await LoadBitmap(resized);
        }
    }
    private async Task<string?> SaveToOriginals(Bitmap bitmap, string filename)
    {
        try
        {
            var directory = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, OriginalsDirectory);
            System.IO.Directory.CreateDirectory(directory);
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

        // Use high-quality interpolation when resizing
        var resized = original.CreateScaledBitmap(
            new PixelSize(newWidth, newHeight),
            BitmapInterpolationMode.HighQuality);

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
            await Task.Delay(100); // Small delay to ensure UI is ready
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
            await Task.Delay(100); // Small delay to ensure UI is ready
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
        
        // Update preview after crop size is set
        if (IsCropStateValid())
        {
            UpdatePreview();
        }
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
            System.Diagnostics.Debug.WriteLine($"TryRestoreSettings failed: settings={settings != null}, bitmap={_currentBitmap != null}");
            return false;
        }
        try
        {
            System.Diagnostics.Debug.WriteLine($"TryRestoreSettings: Attempting to restore settings of type {settings.GetType().Name}");
            var type = settings.GetType();
            var properties = new[]
            {
                "X", "Y", "Width", "Height",
                "ImageDisplayWidth", "ImageDisplayHeight",
                "ImageDisplayOffsetX", "ImageDisplayOffsetY"
            }.Select(name => type.GetProperty(name)).ToArray();
            if (properties.Any(p => p == null))
            {
                System.Diagnostics.Debug.WriteLine("TryRestoreSettings failed: Missing required properties");
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
            
            System.Diagnostics.Debug.WriteLine($"TryRestoreSettings SUCCESS: Restored crop area to X={newX}, Y={newY}, W={newWidth}, H={newHeight}");
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
        double distance = 0;
        double deltaX = 0;
        double deltaY = 0;
        double radius = 0;

        try
        {
            Log.Information("OnCropSelectionPressed called - sender: {Sender}, position: {Position}", 
                sender?.GetType().Name, e.GetPosition(CropSelection));

            if (!ValidateControls(CropSelection, CropOverlay))
            {
                Log.Warning("OnCropSelectionPressed - Invalid controls, returning");
                return;
            }
            if (!IsLeftButtonPressed(e, CropSelection!))
            {
                Log.Warning("OnCropSelectionPressed - Not left button pressed, returning");
                return;
            }

            var position = e.GetPosition(CropSelection);
            var center = new Point(CropSelection!.Width / 2, CropSelection.Height / 2);
            (distance, deltaX, deltaY) = CalculateDistanceAndDeltas(position, center);
            radius = CropSelection.Width / 2;

            Log.Information("OnCropSelectionPressed - position: {Position}, center: {Center}, distance: {Distance}, radius: {Radius}", 
                position, center, distance, radius);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnCropSelectionPressed: {Message}", ex.Message);
            throw;
        }

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
        try
        {
            Log.Information("OnSelectionGroupPressed called - sender: {Sender}, position: {Position}", 
                sender?.GetType().Name, e.GetPosition(CropSelection));

            if (!ValidateControls(CropSelection, CropOverlay))
            {
                Log.Warning("OnSelectionGroupPressed - Invalid controls, returning");
                return;
            }
            if (!IsLeftButtonPressed(e, CropSelection!))
            {
                Log.Warning("OnSelectionGroupPressed - Not left button pressed, returning");
                return;
            }

            Log.Information("OnSelectionGroupPressed - Starting drag operation");
            StartDrag(e, CropOverlay!, CropSelection!);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnSelectionGroupPressed: {Message}", ex.Message);
            throw;
        }
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
        double distance = 0;
        double deltaX = 0;
        double deltaY = 0;

        try
        {
            Log.Information("OnOverlayPressed called - sender: {Sender}, position: {Position}", 
                sender?.GetType().Name, e.GetPosition(CropOverlay));

            if (CropOverlay == null || !IsLeftButtonPressed(e, CropOverlay))
            {
                Log.Warning("OnOverlayPressed - Invalid overlay or not left button pressed, returning");
                return;
            }

            var position = e.GetPosition(CropOverlay);
            var center = new Point(_cropArea.X + _cropArea.Width / 2, _cropArea.Y + _cropArea.Height / 2);
            (distance, deltaX, deltaY) = CalculateDistanceAndDeltas(position, center);

            Log.Information("OnOverlayPressed - position: {Position}, center: {Center}, distance: {Distance}, _cropArea: {CropArea}", 
                position, center, distance, _cropArea);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnOverlayPressed: {Message}", ex.Message);
            throw;
        }

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
            // Mark preview for update when resize ends for better performance
            _isPreviewUpdatePending = true;
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
    private void UpdatePreview()
    {
        // Allow real-time updates during dragging and resizing for better user experience
        _cropPreview?.UpdatePreview(IsCropStateValid() ? CreateCroppedImage() : null);
        _isPreviewUpdatePending = false;
    }
    private Bitmap? CreateCroppedImage()
    {
        try
        {
            Log.Information("CreateCroppedImage called - _cropArea: {CropArea}, _fullResolutionBitmap: {HasFullBitmap}, _currentBitmap: {HasCurrentBitmap}", 
                _cropArea, _fullResolutionBitmap != null, _currentBitmap != null);

            if (!IsCropStateValid())
            {
                Log.Warning("CreateCroppedImage - Crop state invalid, returning null");
                return null;
            }

            // Use full resolution bitmap if available, otherwise fall back to display bitmap
            var sourceBitmap = _fullResolutionBitmap ?? _currentBitmap;
            if (sourceBitmap == null)
            {
                Log.Error("CreateCroppedImage - Both _fullResolutionBitmap and _currentBitmap are null, returning null");
                return null;
            }

            Log.Information("CreateCroppedImage - Using source bitmap with PixelSize: {PixelSize}, _imageDisplaySize: {DisplaySize}", 
                sourceBitmap.PixelSize, _imageDisplaySize);

            // Calculate scale factors based on the full resolution source
            var scaleX = sourceBitmap.PixelSize.Width / _imageDisplaySize.Width;
            var scaleY = sourceBitmap.PixelSize.Height / _imageDisplaySize.Height;
        var cropX = (_cropArea.X - _imageDisplayOffset.X) * scaleX;
        var cropY = (_cropArea.Y - _imageDisplayOffset.Y) * scaleY;
        var cropWidth = _cropArea.Width * scaleX;
        var cropHeight = _cropArea.Height * scaleY;

        // Use a consistent high-quality output size for all profile images
        // This ensures images look sharp even when displayed at large sizes (e.g., 240x240px in ProfileCardLarge)
        var outputSize = ProfileImageOutputSize;

        var renderTarget = new RenderTargetBitmap(new PixelSize(outputSize, outputSize));
        using var drawingContext = renderTarget.CreateDrawingContext();

        // Define the circular clip area for the output
        var clipRect = new Rect(0, 0, outputSize, outputSize);

        using (drawingContext.PushClip(clipRect))
        using (drawingContext.PushGeometryClip(new EllipseGeometry(clipRect)))
        {
            if (Math.Abs(_rotationAngle) <= 0.01)
            {
                // No rotation: Scale the cropped region to fill the output size
                // Source: the actual crop area in the full resolution image
                var sourceRect = new Rect(cropX, cropY, cropWidth, cropHeight);

                // Destination: fill the entire output canvas
                var destRect = new Rect(0, 0, outputSize, outputSize);

                drawingContext.DrawImage(sourceBitmap, sourceRect, destRect);
            }
            else
            {
                // With rotation: apply transform and scale
                var centerX = cropX + cropWidth / 2.0;
                var centerY = cropY + cropHeight / 2.0;
                var scale = outputSize / Math.Max(cropWidth, cropHeight);

                using (drawingContext.PushTransform(
                    Matrix.CreateTranslation(-centerX, -centerY) *
                    Matrix.CreateRotation(_rotationAngle * Math.PI / 180) *
                    Matrix.CreateScale(scale, scale) *
                    Matrix.CreateTranslation(outputSize / 2.0, outputSize / 2.0)))
                {
                    drawingContext.DrawImage(
                        sourceBitmap,
                        new Rect(0, 0, sourceBitmap.PixelSize.Width, sourceBitmap.PixelSize.Height),
                        new Rect(0, 0, sourceBitmap.PixelSize.Width, sourceBitmap.PixelSize.Height)
                    );
                }
            }
        }
        return renderTarget;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in CreateCroppedImage: {Message}", ex.Message);
            return null;
        }
    }
    private void RotateImage(double degrees)
    {
        try
        {
            Log.Information("RotateImage called - degrees: {Degrees}, _currentBitmap: {HasCurrentBitmap}, _fullResolutionBitmap: {HasFullBitmap}", 
                degrees, _currentBitmap != null, _fullResolutionBitmap != null);

            if (_currentBitmap == null || _fullResolutionBitmap == null) 
            {
                Log.Warning("RotateImage - Missing bitmaps, returning");
                return;
            }

            _rotationAngle += degrees;
            
            // Normalize rotation angle to -180 to 180 range
            while (_rotationAngle <= -180) _rotationAngle += 360;
            while (_rotationAngle > 180) _rotationAngle -= 360;

            Log.Information("RotateImage - New rotation angle: {RotationAngle}", _rotationAngle);

            // Apply rotation to the display bitmap
            if (_currentBitmap != null)
            {
                var rotatedDisplay = RotateBitmap(_currentBitmap, (int)degrees);
                if (rotatedDisplay != null)
                {
                    _currentBitmap = rotatedDisplay;
                }
                else
                {
                    Log.Error("RotateImage - Failed to rotate display bitmap");
                    throw new InvalidOperationException("Failed to rotate display bitmap");
                }
            }

            // Apply rotation to the full resolution bitmap
            if (_fullResolutionBitmap != null)
            {
                var rotatedFull = RotateBitmap(_fullResolutionBitmap, (int)degrees);
                if (rotatedFull != null)
                {
                    _fullResolutionBitmap = rotatedFull;
                }
                else
                {
                    Log.Error("RotateImage - Failed to rotate full resolution bitmap");
                    throw new InvalidOperationException("Failed to rotate full resolution bitmap");
                }
            }

            // Update the main image display
            if (MainImage != null && _currentBitmap != null)
            {
                MainImage.Source = _currentBitmap;
            }

            // Update crop area and preview
            UpdateCropSize();
            ApplyCropTransform();
            UpdatePreview();
            
            // Reset drag state to prevent issues with stale coordinates
            _isDragging = false;
            _isResizing = false;

            Log.Information("RotateImage completed successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in RotateImage: {Message}", ex.Message);
            // Reset rotation angle on error
            _rotationAngle -= degrees;
            throw;
        }
    }
    #endregion
    #region Button Handlers
    private void ResetImageState()
    {
        _currentBitmap?.Dispose();
        _currentBitmap = null;
        _fullResolutionBitmap?.Dispose();
        _fullResolutionBitmap = null;
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
            // Create and save the cropped image
            var croppedImage = CreateCroppedImage();
            if (croppedImage != null)
            {
                using var stream = File.Create(savePath);
                croppedImage.Save(stream);
            }

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
        
        // Add a small delay to ensure the file is fully written before updating preview
        await Task.Delay(100);
        
        // Update the crop preview to use the SAME final file path AFTER the file is written
        System.Diagnostics.Debug.WriteLine($"ImageCropWindow.HandleSaveButtonAsync calling UpdatePreviewFromPath with: {savePath}");
        _cropPreview?.UpdatePreviewFromPath(savePath);
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
    #region EXIF Orientation Handling
    private static Bitmap LoadBitmapWithCorrectOrientation(Stream stream, string? filePath)
    {
        Log.Information("LoadBitmapWithCorrectOrientation called with filePath: {FilePath}", filePath ?? "null");

        // Read EXIF orientation if available
        int orientation = 1; // Default: no rotation needed

        // Try reading EXIF from file path first
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            try
            {
                Log.Information("Attempting to read EXIF metadata from file...");
                var directories = ImageMetadataReader.ReadMetadata(filePath);
                Log.Information("Found {DirectoryCount} metadata directories", directories.Count());

                var exifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                Log.Information("ExifSubIfdDirectory found: {Found}", exifSubIfdDirectory != null);

                if (exifSubIfdDirectory != null)
                {
                    var hasOrientation = exifSubIfdDirectory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientationValue);
                    Log.Information("Orientation tag present: {HasOrientation}", hasOrientation);

                    if (hasOrientation)
                    {
                        orientation = orientationValue;
                        Log.Information("EXIF Orientation detected: {Orientation} from file: {FilePath}", orientation, filePath);
                    }
                    else
                    {
                        Log.Information("No orientation tag found in EXIF data");
                    }
                }
                else
                {
                    Log.Information("No ExifSubIfdDirectory found, checking for ExifIfd0Directory...");
                    var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();

                    if (exifIfd0Directory != null)
                    {
                        var hasOrientation = exifIfd0Directory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientationValue);
                        Log.Information("IFD0 Orientation tag present: {HasOrientation}", hasOrientation);

                        if (hasOrientation)
                        {
                            orientation = orientationValue;
                            Log.Information("EXIF Orientation detected in IFD0: {Orientation}", orientation);
                        }
                    }
                    else
                    {
                        Log.Information("No EXIF directories found at all");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read EXIF from file path: {FilePath}", filePath);
            }
        }
        // If no file path, try reading from stream
        else if (stream.CanSeek)
        {
            try
            {
                var position = stream.Position;
                var directories = ImageMetadataReader.ReadMetadata(stream);
                stream.Position = position; // Reset stream position

                var exifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

                if (exifSubIfdDirectory != null && exifSubIfdDirectory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientationValue))
                {
                    orientation = orientationValue;
                    Log.Information("EXIF Orientation detected: {Orientation} from stream", orientation);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read EXIF from stream");
            }
        }

        // Load the bitmap
        var originalBitmap = new Bitmap(stream);

        // Apply rotation based on EXIF orientation
        if (orientation != 1)
        {
            Log.Information("EXIF Orientation detected: {Orientation}, but skipping auto-rotation to preserve original orientation", orientation);
        }

        // Always return the original bitmap without EXIF-based rotation
        // This prevents unwanted rotations when the image is already correctly oriented
        return originalBitmap;
    }

    private static Bitmap RotateBitmap(Bitmap source, int degrees)
    {
        if (source == null)
        {
            System.Diagnostics.Debug.WriteLine("RotateBitmap: source is null");
            return source!;
        }

        if (degrees % 360 == 0)
            return source;

        try
        {
            // Normalize degrees to 0-360 range for easier handling
            int normalizedDegrees = ((degrees % 360) + 360) % 360;
            
            // Calculate new dimensions based on rotation
            int newWidth, newHeight;
            if (normalizedDegrees == 90 || normalizedDegrees == 270)
            {
                // Swap dimensions for 90° and 270° rotations
                newWidth = source.PixelSize.Height;
                newHeight = source.PixelSize.Width;
            }
            else
            {
                newWidth = source.PixelSize.Width;
                newHeight = source.PixelSize.Height;
            }

        // Create a new bitmap with rotated dimensions
        var rotated = new RenderTargetBitmap(new PixelSize(newWidth, newHeight));

        using (var context = rotated.CreateDrawingContext())
        {
            // Apply rotation transformation
            var matrix = Matrix.Identity;

            switch (normalizedDegrees)
            {
                case 90:
                    matrix = Matrix.CreateTranslation(-source.PixelSize.Width / 2.0, -source.PixelSize.Height / 2.0) *
                             Matrix.CreateRotation(Math.PI / 2) *
                             Matrix.CreateTranslation(newWidth / 2.0, newHeight / 2.0);
                    break;
                case 180:
                    matrix = Matrix.CreateTranslation(-source.PixelSize.Width / 2.0, -source.PixelSize.Height / 2.0) *
                             Matrix.CreateRotation(Math.PI) *
                             Matrix.CreateTranslation(newWidth / 2.0, newHeight / 2.0);
                    break;
                case 270:
                    matrix = Matrix.CreateTranslation(-source.PixelSize.Width / 2.0, -source.PixelSize.Height / 2.0) *
                             Matrix.CreateRotation(3 * Math.PI / 2) *
                             Matrix.CreateTranslation(newWidth / 2.0, newHeight / 2.0);
                    break;
            }

            using (context.PushTransform(matrix))
            {
                context.DrawImage(source,
                    new Rect(0, 0, source.PixelSize.Width, source.PixelSize.Height),
                    new Rect(0, 0, source.PixelSize.Width, source.PixelSize.Height));
            }
        }

            // Dispose the original bitmap
            source.Dispose();

            return rotated;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in RotateBitmap: {ex.Message}");
            return source; // Return original bitmap on error
        }
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
        try
        {
            Log.Information("StartResize called - deltaX: {DeltaX}, deltaY: {DeltaY}, _cropArea: {CropArea}", 
                deltaX, deltaY, _cropArea);

            _isResizing = true;
            _pointerStartPosition = e.GetPosition(overlay);
            _dragStartCropArea = _cropArea;
            cursor.Cursor = new Cursor(GetResizeCursor(deltaX, deltaY));
            e.Pointer.Capture(cursor);
            e.Handled = true;

            Log.Information("StartResize completed - _isResizing: {IsResizing}, _pointerStartPosition: {StartPosition}", 
                _isResizing, _pointerStartPosition);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in StartResize: {Message}", ex.Message);
            throw;
        }
    }

    private void StartDrag(PointerPressedEventArgs e, Control overlay, Control cursor)
    {
        try
        {
            Log.Information("StartDrag called - _cropArea: {CropArea}", _cropArea);

            _isDragging = true;
            _pointerStartPosition = e.GetPosition(overlay);
            _dragStartCropArea = _cropArea;
            cursor.Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(cursor);
            e.Handled = true;

            Log.Information("StartDrag completed - _isDragging: {IsDragging}, _pointerStartPosition: {StartPosition}", 
                _isDragging, _pointerStartPosition);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in StartDrag: {Message}", ex.Message);
            throw;
        }
    }

    private void EndResize()
    {
        _isResizing = false;
        
        if (_isPreviewUpdatePending)
        {
            _cropPreview?.UpdatePreview(IsCropStateValid() ? CreateCroppedImage() : null);
            _isPreviewUpdatePending = false;
        }
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
        
        if (_isPreviewUpdatePending)
        {
            _cropPreview?.UpdatePreview(IsCropStateValid() ? CreateCroppedImage() : null);
            _isPreviewUpdatePending = false;
        }
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
        try
        {
            Log.Information("PerformDrag called - currentPosition: {Position}, _pointerStartPosition: {StartPosition}, _dragStartCropArea: {StartCropArea}", 
                currentPosition, _pointerStartPosition, _dragStartCropArea);

            if (!IsCropStateValid()) 
            {
                Log.Warning("PerformDrag - Crop state invalid, returning");
                return;
            }
            
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
            
            // Ensure the crop area is within valid bounds
            var cropWidth = Math.Max(1, Math.Min(_dragStartCropArea.Width, _imageDisplaySize.Width));
            var cropHeight = Math.Max(1, Math.Min(_dragStartCropArea.Height, _imageDisplaySize.Height));
            
            _cropArea = new Rect(newX, newY, cropWidth, cropHeight);
            ApplyCropTransform();
            // Mark preview for update when drag ends for better performance
            _isPreviewUpdatePending = true;

            Log.Information("PerformDrag completed - new _cropArea: {CropArea}", _cropArea);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in PerformDrag: {Message}", ex.Message);
            throw;
        }
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
        try
        {
            Log.Information("PerformResize called - currentPosition: {Position}, _pointerStartPosition: {StartPosition}, _dragStartCropArea: {StartCropArea}", 
                currentPosition, _pointerStartPosition, _dragStartCropArea);

            if (!IsCropStateValid()) 
            {
                Log.Warning("PerformResize - Crop state invalid, returning");
                return;
            }
            
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
            
            // Ensure minimum size
            halfNewSize = Math.Max(halfNewSize, MinimumCropSize / 2);
            
            _cropArea = new Rect(centerX - halfNewSize, centerY - halfNewSize, halfNewSize * 2, halfNewSize * 2);
            ApplyCropTransform();
            // Mark preview for update when resize ends for better performance
            _isPreviewUpdatePending = true;

            Log.Information("PerformResize completed - new _cropArea: {CropArea}, halfNewSize: {HalfNewSize}", _cropArea, halfNewSize);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in PerformResize: {Message}", ex.Message);
            throw;
        }
    }

    private void SetPreviewState(bool showActions, bool resetEnabled, bool saveEnabled)
    {
        _cropPreview?.ShowActions(showActions);
        _cropPreview?.SetResetEnabled(resetEnabled);
        _cropPreview?.SetSaveEnabled(saveEnabled);
        _cropPreview?.SetRotateLeftEnabled(showActions && _currentBitmap != null);
        _cropPreview?.SetRotateRightEnabled(showActions && _currentBitmap != null);
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
            System.IO.Directory.CreateDirectory(directory);
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
            if (!System.IO.Directory.Exists(directory))
            {
                return Task.FromResult(Array.Empty<string>());
            }
            var imageFiles = System.IO.Directory.GetFiles(directory)
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
        var isValid = _currentBitmap != null
            && _cropArea.Width > 0
            && _cropArea.Height > 0
            && _imageDisplaySize.Width > 0
            && _imageDisplaySize.Height > 0;

        if (!isValid)
        {
            Log.Warning("IsCropStateValid failed - _currentBitmap: {HasBitmap}, _cropArea: {CropArea}, _imageDisplaySize: {DisplaySize}", 
                _currentBitmap != null, _cropArea, _imageDisplaySize);
        }

        return isValid;
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
