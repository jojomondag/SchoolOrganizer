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
using SchoolOrganizer.Src.ViewModels;
using SchoolOrganizer.Src.Views.Windows.ImageCrop;

namespace SchoolOrganizer.Src.Views.ImageCrop;

public partial class ImageCropView : UserControl
{
    #region Constants
    private const double MinimumCropSize = 50;
    private const double MaximumCropSize = 400;
    private const double MaximumCropRatio = 0.8;
    private const double SidebarWidth = 260;
    private const double ScreenMargin = 60;
    private const int MaximumImageSize = 4096;
    private const int ProfileImageOutputSize = 512;
    private static readonly string[] SupportedImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
    private const string OriginalsDirectory = "Data/ProfileImages/Originals";
    private const string ProfileDirectory = "Data/ProfileImages";
    #endregion

    #region Fields
    private Bitmap? _currentBitmap;
    private Bitmap? _fullResolutionBitmap;
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
    private string? _currentOriginalImagePath;

    // Performance optimization fields
    private DateTime _lastPreviewUpdate = DateTime.MinValue;
    private const int PreviewThrottleMs = 33; // 30fps throttling
    #endregion

    #region Properties
    public Func<Task<string?>>? SavePathProvider { get; set; }
    public Func<Task<string[]>>? AvailableImagesProvider { get; set; }
    public Func<string, Task<object?>>? CropSettingsProvider { get; set; }
    public event EventHandler<string>? ImageSaved;
    public event EventHandler<IStorageFile>? OriginalImageSelected;
    public event EventHandler? CancelRequested;

    public ImageCropViewModel? ViewModel => DataContext as ImageCropViewModel;

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
    public ImageCropView()
    {
        InitializeComponent();
        InitializeMainImageDisplay();
        InitializeEventHandlers();
        AttachKeyHandlers();
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
            _cropPreview.CancelClicked += (s, e) => HandleCancel();
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

        Loaded += async (s, e) => await LoadGallery();
        OriginalImageSelected += async (s, file) => await LoadGallery();
        SavePathProvider = PrepareSavePath;
        AvailableImagesProvider = GetAvailableImages;
    }

    private void AttachKeyHandlers()
    {
        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                HandleCancel();
                e.Handled = true;
            }
        };
    }

    private void HandleCancel()
    {
        // Only clear the image, don't close the entire crop view
        ResetImageState();
    }
    #endregion

    #region Public Methods
    public async Task LoadImageForStudentAsync(int studentId, string? existingOriginalImagePath = null, string? cropSettingsJson = null)
    {
        if (ViewModel != null)
        {
            ViewModel.InitializeForStudent(studentId, existingOriginalImagePath, cropSettingsJson);
        }

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
                }
            }

            await LoadImageFromPathWithCropSettingsAsync(existingOriginalImagePath, settings);
        }
        else
        {
            // If no original image path is provided, load the first available image from the gallery
            System.Diagnostics.Debug.WriteLine($"No original image path provided for student {studentId}, loading first available image from gallery");
            await LoadGallery();
            
            // Try to load the first image from the gallery if available
            if (AvailableImagesProvider != null)
            {
                var imagePaths = await AvailableImagesProvider();
                if (imagePaths != null && imagePaths.Length > 0)
                {
                    var firstImagePath = imagePaths.FirstOrDefault(File.Exists);
                    if (!string.IsNullOrEmpty(firstImagePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"Loading first available image: {firstImagePath}");
                        await LoadImageFromPathWithCropSettingsAsync(firstImagePath, null);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No valid images found in gallery");
                    }
                }
            }
        }
    }

    public async Task LoadImageFromPathAsync(string path)
    {
        await LoadImageFromPathWithCropSettingsAsync(path, null);
    }

    public async Task LoadImageFromPathWithCropSettingsAsync(string path, object? settings)
    {
        if (!File.Exists(path)) return;

        _currentOriginalImagePath = path;

        Log.Information("Loading image from path: {Path}", path);
        await using var fileStream = File.OpenRead(path);
        var bitmap = LoadBitmapWithCorrectOrientation(fileStream, path);

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
        var topLevel = TopLevel.GetTopLevel(this);
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
        await Task.Delay(200);
        if (settings != null && TryRestoreSettings(settings))
        {
            ApplyCropTransform();
            await Task.Delay(100);
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
            await Task.Delay(100);
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

    // I'll continue with the rest of the code in the next part...
    // The file is getting long, so I'm splitting it into parts

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
        return Math.Min(MaximumCropSize, maxFromImage);
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
        double distance = 0;
        double deltaX = 0;
        double deltaY = 0;
        double radius = 0;

        try
        {
            if (!ValidateControls(CropSelection, CropOverlay))
            {
                return;
            }
            if (!IsLeftButtonPressed(e, CropSelection!))
            {
                return;
            }

            var position = e.GetPosition(CropSelection);
            var center = new Point(CropSelection!.Width / 2, CropSelection.Height / 2);
            (distance, deltaX, deltaY) = CalculateDistanceAndDeltas(position, center);
            radius = CropSelection.Width / 2;
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
        UpdatePreviewLive();
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
            if (!ValidateControls(CropSelection, CropOverlay))
            {
                return;
            }
            if (!IsLeftButtonPressed(e, CropSelection!))
            {
                return;
            }

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
        UpdatePreviewLive();
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
            if (CropOverlay == null || !IsLeftButtonPressed(e, CropOverlay))
            {
                return;
            }

            var position = e.GetPosition(CropOverlay);
            var center = new Point(_cropArea.X + _cropArea.Width / 2, _cropArea.Y + _cropArea.Height / 2);
            (distance, deltaX, deltaY) = CalculateDistanceAndDeltas(position, center);
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
            UpdatePreviewLive();
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
            UpdatePreviewLive();
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
        _cropPreview?.UpdatePreview(IsCropStateValid() ? CreateCroppedImage() : null);
    }

    private void UpdatePreviewLive()
    {
        var now = DateTime.Now;
        if ((now - _lastPreviewUpdate).TotalMilliseconds < PreviewThrottleMs)
        {
            return;
        }

        if (!IsCropStateValid())
        {
            return;
        }

        _lastPreviewUpdate = now;

        var preview = CreateCroppedImageFast();
        if (preview != null)
        {
            _cropPreview?.UpdatePreviewDirect(preview);
        }
    }

    private Bitmap? CreateCroppedImage()
    {
        return CreateCroppedImageInternal(false);
    }

    private Bitmap? CreateCroppedImageFast()
    {
        try
        {
            if (!IsCropStateValid())
            {
                return null;
            }

            var sourceBitmap = _currentBitmap;
            if (sourceBitmap == null)
            {
                return null;
            }

            const int previewSize = 140;

            var scaleX = sourceBitmap.PixelSize.Width / _imageDisplaySize.Width;
            var scaleY = sourceBitmap.PixelSize.Height / _imageDisplaySize.Height;
            var cropX = (_cropArea.X - _imageDisplayOffset.X) * scaleX;
            var cropY = (_cropArea.Y - _imageDisplayOffset.Y) * scaleY;
            var cropWidth = _cropArea.Width * scaleX;
            var cropHeight = _cropArea.Height * scaleY;

            var renderTarget = new RenderTargetBitmap(new PixelSize(previewSize, previewSize));
            using var drawingContext = renderTarget.CreateDrawingContext();

            var clipRect = new Rect(0, 0, previewSize, previewSize);
            using (drawingContext.PushClip(clipRect))
            using (drawingContext.PushGeometryClip(new EllipseGeometry(clipRect)))
            {
                if (Math.Abs(_rotationAngle) <= 0.01)
                {
                    var sourceRect = new Rect(cropX, cropY, cropWidth, cropHeight);
                    var destRect = new Rect(0, 0, previewSize, previewSize);
                    drawingContext.DrawImage(sourceBitmap, sourceRect, destRect);
                }
                else
                {
                    var centerX = cropX + cropWidth / 2.0;
                    var centerY = cropY + cropHeight / 2.0;
                    var scale = previewSize / Math.Max(cropWidth, cropHeight);

                    using (drawingContext.PushTransform(
                        Matrix.CreateTranslation(-centerX, -centerY) *
                        Matrix.CreateRotation(_rotationAngle * Math.PI / 180) *
                        Matrix.CreateScale(scale, scale) *
                        Matrix.CreateTranslation(previewSize / 2.0, previewSize / 2.0)))
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
            Log.Error(ex, "Error in CreateCroppedImageFast: {Message}", ex.Message);
            return null;
        }
    }

    private Bitmap? CreateCroppedImageInternal(bool lowRes)
    {
        try
        {
            if (!IsCropStateValid())
            {
                return null;
            }

            var sourceBitmap = _fullResolutionBitmap ?? _currentBitmap;
            if (sourceBitmap == null)
            {
                return null;
            }

            var scaleX = sourceBitmap.PixelSize.Width / _imageDisplaySize.Width;
            var scaleY = sourceBitmap.PixelSize.Height / _imageDisplaySize.Height;
            var cropX = (_cropArea.X - _imageDisplayOffset.X) * scaleX;
            var cropY = (_cropArea.Y - _imageDisplayOffset.Y) * scaleY;
            var cropWidth = _cropArea.Width * scaleX;
            var cropHeight = _cropArea.Height * scaleY;

            var outputSize = lowRes ? 256 : ProfileImageOutputSize;

            var renderTarget = new RenderTargetBitmap(new PixelSize(outputSize, outputSize));
            using var drawingContext = renderTarget.CreateDrawingContext();

            var clipRect = new Rect(0, 0, outputSize, outputSize);

            using (drawingContext.PushClip(clipRect))
            using (drawingContext.PushGeometryClip(new EllipseGeometry(clipRect)))
            {
                if (Math.Abs(_rotationAngle) <= 0.01)
                {
                    var sourceRect = new Rect(cropX, cropY, cropWidth, cropHeight);
                    var destRect = new Rect(0, 0, outputSize, outputSize);
                    drawingContext.DrawImage(sourceBitmap, sourceRect, destRect);
                }
                else
                {
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
            Log.Error(ex, "Error in CreateCroppedImageInternal: {Message}", ex.Message);
            return null;
        }
    }

    private void RotateImage(double degrees)
    {
        try
        {
            if (_currentBitmap == null || _fullResolutionBitmap == null)
            {
                return;
            }

            _rotationAngle += degrees;

            while (_rotationAngle <= -180) _rotationAngle += 360;
            while (_rotationAngle > 180) _rotationAngle -= 360;

            if (_currentBitmap != null)
            {
                var rotatedDisplay = RotateBitmap(_currentBitmap, (int)degrees);
                if (rotatedDisplay != null)
                {
                    _currentBitmap = rotatedDisplay;
                }
                else
                {
                    throw new InvalidOperationException("Failed to rotate display bitmap");
                }
            }

            if (_fullResolutionBitmap != null)
            {
                var rotatedFull = RotateBitmap(_fullResolutionBitmap, (int)degrees);
                if (rotatedFull != null)
                {
                    _fullResolutionBitmap = rotatedFull;
                }
                else
                {
                    throw new InvalidOperationException("Failed to rotate full resolution bitmap");
                }
            }

            if (MainImage != null && _currentBitmap != null)
            {
                MainImage.Source = _currentBitmap;
            }

            UpdateCropSize();
            ApplyCropTransform();
            UpdatePreview();

            _isDragging = false;
            _isResizing = false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in RotateImage: {Message}", ex.Message);
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
        
        // Clear the main image display using the new method
        _mainImageDisplay?.ClearImage();
        
        _cropPreview?.UpdatePreview(null);
        SetPreviewState(false, false, false);
    }

    private async Task HandleSaveButtonAsync()
    {
        if (_currentBitmap == null) return;

        var savePath = SavePathProvider != null ? await SavePathProvider() : null;
        if (string.IsNullOrEmpty(savePath)) return;

        TryExecute(() =>
        {
            var croppedImage = CreateCroppedImage();
            if (croppedImage != null)
            {
                using var stream = File.Create(savePath);
                croppedImage.Save(stream);
            }

            var settings = GetCurrentCropSettings();
            var settingsJson = settings != null ? JsonSerializer.Serialize(settings) : null;

            ViewModel?.NotifyImageSaved(savePath, settingsJson, _currentOriginalImagePath);
            ImageSaved?.Invoke(this, savePath);
        });

        await Task.Delay(100);

        _cropPreview?.UpdatePreviewFromPath(savePath);
    }
    #endregion

    #region EXIF Orientation Handling
    private static Bitmap LoadBitmapWithCorrectOrientation(Stream stream, string? filePath)
    {
        int orientation = 1;

        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(filePath);
                var exifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

                if (exifSubIfdDirectory != null && exifSubIfdDirectory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientationValue))
                {
                    orientation = orientationValue;
                }
                else
                {
                    var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                    if (exifIfd0Directory != null && exifIfd0Directory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientationValue0))
                    {
                        orientation = orientationValue0;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read EXIF from file path: {FilePath}", filePath);
            }
        }
        else if (stream.CanSeek)
        {
            try
            {
                var position = stream.Position;
                var directories = ImageMetadataReader.ReadMetadata(stream);
                stream.Position = position;

                var exifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (exifSubIfdDirectory != null && exifSubIfdDirectory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientationValue))
                {
                    orientation = orientationValue;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read EXIF from stream");
            }
        }

        var originalBitmap = new Bitmap(stream);
        return originalBitmap;
    }

    private static Bitmap RotateBitmap(Bitmap source, int degrees)
    {
        if (source == null)
        {
            return source!;
        }

        if (degrees % 360 == 0)
            return source;

        try
        {
            int normalizedDegrees = ((degrees % 360) + 360) % 360;

            int newWidth, newHeight;
            if (normalizedDegrees == 90 || normalizedDegrees == 270)
            {
                newWidth = source.PixelSize.Height;
                newHeight = source.PixelSize.Width;
            }
            else
            {
                newWidth = source.PixelSize.Width;
                newHeight = source.PixelSize.Height;
            }

            var rotated = new RenderTargetBitmap(new PixelSize(newWidth, newHeight));

            using (var context = rotated.CreateDrawingContext())
            {
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

            source.Dispose();
            return rotated;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in RotateBitmap: {ex.Message}");
            return source;
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
            _isResizing = true;
            _pointerStartPosition = e.GetPosition(overlay);
            _dragStartCropArea = _cropArea;
            cursor.Cursor = new Cursor(GetResizeCursor(deltaX, deltaY));
            e.Pointer.Capture(cursor);
            e.Handled = true;
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
            _isDragging = true;
            _pointerStartPosition = e.GetPosition(overlay);
            _dragStartCropArea = _cropArea;
            cursor.Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(cursor);
            e.Handled = true;
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

        Dispatcher.UIThread.Post(() =>
        {
            if (IsCropStateValid())
            {
                _cropPreview?.UpdatePreview(CreateCroppedImage());
            }
        }, DispatcherPriority.Render);
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
            if (!IsCropStateValid())
            {
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

            var cropWidth = Math.Max(1, Math.Min(_dragStartCropArea.Width, _imageDisplaySize.Width));
            var cropHeight = Math.Max(1, Math.Min(_dragStartCropArea.Height, _imageDisplaySize.Height));

            _cropArea = new Rect(newX, newY, cropWidth, cropHeight);
            ApplyCropTransform();
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
    }

    private void PerformResize(Point currentPosition)
    {
        try
        {
            if (!IsCropStateValid())
            {
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

            halfNewSize = Math.Max(halfNewSize, MinimumCropSize / 2);

            _cropArea = new Rect(centerX - halfNewSize, centerY - halfNewSize, halfNewSize * 2, halfNewSize * 2);
            ApplyCropTransform();
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
    #endregion

    #region Utility Methods
    private Task<string?> PrepareSavePath()
    {
        try
        {
            var directory = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, ProfileDirectory);
            System.IO.Directory.CreateDirectory(directory);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var filename = ViewModel?.StudentId.HasValue == true
                ? $"student_{ViewModel.StudentId.Value}_{timestamp}.png"
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
}
