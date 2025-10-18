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
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Serilog;

namespace SchoolOrganizer.Src.Views.Windows.ImageCrop;

public partial class ConsolidatedImageCropWindow : Window
{
    private const double MinCropSize = 50;
    private const double MaxCropSize = 400;
    private const int ThumbSize = 90;
    private const int ProfileImageOutputSize = 512; // High-quality output size for profile images
    private const int MaxDisplayImageSize = 4096; // Maximum size for display in UI - allow much larger images to preserve quality

    private Bitmap? _currentBitmap;
    private Bitmap? _fullResolutionBitmap; // Keep full resolution version for high-quality final crop
    private bool _isDragging;
    private bool _isResizing;
    private bool _isPreviewUpdatePending;
    private string? _activeHandle;
    private Rect _cropArea;
    private Rect _dragStartCropArea;
    private Point _pointerStartPosition;
    private Size _imageDisplaySize;
    private Point _imageDisplayOffset;
    private double _rotationAngle = 0;
    private double _rotationStartAngle;
    private double _rotationInitialAngle;

    private Grid? _imageGalleryGrid;
    private string? _tempPreviewPath;
    
    public string? SavedImagePath { get; private set; }
    public string? SavedCropSettings { get; private set; }
    public string? SavedOriginalImagePath { get; private set; }

    public ConsolidatedImageCropWindow()
    {
        InitializeComponent();
        InitializeControls();
        InitializeEventHandlers();
    }

    private void InitializeControls()
    {
        _imageGalleryGrid = this.FindControl<Grid>("ImageGalleryGrid");
    }

    private void InitializeEventHandlers()
    {
        // Image area events
        ImageArea.PointerPressed += OnImageAreaPressed;
        ImageArea.PointerMoved += OnImageAreaMoved;
        ImageArea.PointerReleased += OnImageAreaReleased;
        
        // Crop selection events
        CropSelection.PointerPressed += OnCropSelectionPressed;
        CropSelection.PointerMoved += OnCropSelectionMoved;
        CropSelection.PointerReleased += OnCropSelectionReleased;
        
        // Handle events
        AttachHandleEvents();
        
        // Button events
        SelectButton.Click += OnSelectImage;
        CancelButton.Click += OnCancel;
        RotateLeftButton.Click += OnRotateLeftClick;
        RotateRightButton.Click += OnRotateRightClick;
        
        // Window events
        KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private void AttachHandleEvents()
    {
        var handles = new[] { HandleTopLeft, HandleTopRight, HandleBottomLeft, HandleBottomRight };
        var positions = new[] { "tl", "tr", "bl", "br" };
        
        for (int i = 0; i < 4; i++)
        {
            var handle = handles[i];
            var position = positions[i];
            
            handle.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
                {
                    StartRotation(e, position);
                    e.Handled = true;
                }
            };
        }
    }

    public static async Task<string?> ShowAsync(Window parent)
    {
        var dialog = new ConsolidatedImageCropWindow();
        await dialog.ShowDialog(parent);
        return dialog.SavedImagePath;
    }

    public static async Task<(string? imagePath, string? cropSettings, string? originalImagePath)> ShowForStudentAsync(
        Window parent, int studentId, string? existingOriginalImagePath = null, string? cropSettingsJson = null)
    {
        var dialog = new ConsolidatedImageCropWindow();
        await dialog.ShowDialog(parent);
        return (dialog.SavedImagePath, dialog.SavedCropSettings, dialog.SavedOriginalImagePath);
    }

    private async void OnSelectImage(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var filePickerOptions = new FilePickerOpenOptions
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
        };

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(filePickerOptions);
        if (files.Count > 0)
        {
            await LoadImageFromFile(files[0]);
        }
    }

    private async Task LoadImageFromFile(IStorageFile file)
    {
        try
        {
            using var stream = await file.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var bitmap = LoadBitmapWithCorrectOrientation(memoryStream, file.Path.LocalPath);

            // Keep full resolution bitmap for high-quality final crop
            _fullResolutionBitmap?.Dispose();
            _fullResolutionBitmap = bitmap;

            // Create a resized version for display if needed
            var displayBitmap = ResizeImageIfNeeded(bitmap);
            await LoadBitmap(displayBitmap);
            await LoadImageGallery();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading image: {ex.Message}");
        }
    }

    private Bitmap ResizeImageIfNeeded(Bitmap original)
    {
        var width = original.PixelSize.Width;
        var height = original.PixelSize.Height;
        if (width <= MaxDisplayImageSize && height <= MaxDisplayImageSize)
        {
            return original;
        }
        double scale = width > height
            ? (double)MaxDisplayImageSize / width
            : (double)MaxDisplayImageSize / height;
        var newWidth = (int)(width * scale);
        var newHeight = (int)(height * scale);

        // Use high-quality interpolation when resizing
        var resized = original.CreateScaledBitmap(
            new PixelSize(newWidth, newHeight),
            BitmapInterpolationMode.HighQuality);

        return resized;
    }

    private async Task LoadBitmap(Bitmap bitmap)
    {
        _currentBitmap?.Dispose();
        _currentBitmap = bitmap;

        MainImage.Source = _currentBitmap;
        MainImage.IsVisible = true;
        BackgroundPattern.IsVisible = false;
        CropOverlay.IsVisible = true;

        UpdatePreviewState(true, true, true);
        await Task.Delay(100);

        InitializeCropArea();
        UpdatePreview();
    }

    private void InitializeCropArea()
    {
        if (_currentBitmap == null) return;
        
        var imageBounds = MainImage.Bounds;
        if (imageBounds.Width <= 0 || imageBounds.Height <= 0) return;
        
        var imageSize = _currentBitmap.PixelSize;
        var (displaySize, offset) = CalculateFitSize(imageBounds, imageSize);
        _imageDisplaySize = displaySize;
        _imageDisplayOffset = offset;
        
        var cropSize = Math.Min(200, Math.Min(displaySize.Width, displaySize.Height) * 0.6);
        cropSize = Math.Clamp(cropSize, MinCropSize, MaxCropSize);
        
        var centerX = offset.X + displaySize.Width / 2;
        var centerY = offset.Y + displaySize.Height / 2;
        
        _cropArea = new Rect(
            centerX - cropSize / 2,
            centerY - cropSize / 2,
            cropSize,
            cropSize
        );
        
        ApplyCropTransform();
        UpdateOverlay();
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

    private void ApplyCropTransform()
    {
        if (SelectionGroup == null || CropSelection == null) return;
        
        var snappedRect = SnapToPixels(_cropArea);
        SelectionGroup.Width = snappedRect.Width;
        SelectionGroup.Height = snappedRect.Height;
        SelectionGroup.Margin = new Thickness(snappedRect.X, snappedRect.Y, 0, 0);
        CropSelection.Width = snappedRect.Width;
        CropSelection.Height = snappedRect.Height;
        CropSelection.CornerRadius = new CornerRadius(snappedRect.Width / 2);
        
        UpdateRotation();
        UpdateOverlay();
    }

    private void UpdateRotation()
    {
        if (SelectionGroup == null) return;
        
        SelectionGroup.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        SelectionGroup.RenderTransform = new RotateTransform(_rotationAngle);
        PositionHandles();
    }

    private void PositionHandles()
    {
        if (SelectionGroup == null) return;
        
        var width = SelectionGroup.Width;
        var center = width / 2;
        var radius = width / 2;
        var angles = new[] { 45.0, 135.0, 225.0, 315.0 };
        var handles = new[] { HandleTopLeft, HandleTopRight, HandleBottomLeft, HandleBottomRight };
        
        for (int i = 0; i < 4; i++)
        {
            var handle = handles[i];
            if (handle == null) continue;
            
            var angleInRadians = angles[i] * Math.PI / 180;
            var x = center + radius * Math.Cos(angleInRadians) - 7;
            var y = center + radius * Math.Sin(angleInRadians) - 7;
            Canvas.SetLeft(handle, x);
            Canvas.SetTop(handle, y);
        }
    }

    private void UpdateOverlay()
    {
        if (OverlayCutout == null) return;
        
        var outerRect = new Rect(0, 0, ImageArea.Bounds.Width, ImageArea.Bounds.Height);
        var center = _cropArea.Center;
        var radius = _cropArea.Width / 2;
        
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

    private void OnImageAreaPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_currentBitmap == null || !e.GetCurrentPoint(ImageArea).Properties.IsLeftButtonPressed)
            return;

        var position = e.GetPosition(ImageArea);
        var center = _cropArea.Center;
        var distance = Math.Sqrt(Math.Pow(position.X - center.X, 2) + Math.Pow(position.Y - center.Y, 2));
        var radius = _cropArea.Width / 2;

        if (distance > radius - 20 && distance < radius + 10)
        {
            StartResize(e);
        }
    }

    private void OnImageAreaMoved(object? sender, PointerEventArgs e)
    {
        if (!_isResizing || !e.GetCurrentPoint(ImageArea).Properties.IsLeftButtonPressed)
            return;

        PerformResize(e.GetPosition(ImageArea));
        ApplyCropTransform();
        // Mark preview for update when resize ends for better performance
        _isPreviewUpdatePending = true;
    }

    private void OnImageAreaReleased(object? sender, PointerReleasedEventArgs e)
    {
        EndResize();
    }

    private void OnCropSelectionPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_currentBitmap == null || !e.GetCurrentPoint(CropSelection).Properties.IsLeftButtonPressed)
            return;

        StartDrag(e);
    }

    private void OnCropSelectionMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || !e.GetCurrentPoint(CropSelection).Properties.IsLeftButtonPressed)
            return;

        PerformDrag(e.GetPosition(ImageArea));
        ApplyCropTransform();
        // Mark preview for update when drag ends for better performance
        _isPreviewUpdatePending = true;
    }

    private void OnCropSelectionReleased(object? sender, PointerReleasedEventArgs e)
    {
        EndDrag();
    }

    private void StartDrag(PointerPressedEventArgs e)
    {
        _isDragging = true;
        _pointerStartPosition = e.GetPosition(ImageArea);
        _dragStartCropArea = _cropArea;
        e.Pointer.Capture(CropSelection);
        e.Handled = true;
    }

    private void StartResize(PointerPressedEventArgs e)
    {
        _isResizing = true;
        _pointerStartPosition = e.GetPosition(ImageArea);
        _dragStartCropArea = _cropArea;
        e.Pointer.Capture(ImageArea);
        e.Handled = true;
    }

    private void StartRotation(PointerPressedEventArgs e, string handlePosition)
    {
        _activeHandle = handlePosition;
        _pointerStartPosition = e.GetPosition(ImageArea);
        _rotationStartAngle = CalculateAngle(_cropArea.Center, _pointerStartPosition);
        _rotationInitialAngle = _rotationAngle;
        e.Pointer.Capture(ImageArea);
        e.Handled = true;
    }

    private void PerformDrag(Point currentPosition)
    {
        if (_currentBitmap == null || _imageDisplaySize.Width <= 0 || _imageDisplaySize.Height <= 0) return;
        
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
    }

    private void PerformResize(Point currentPosition)
    {
        if (_currentBitmap == null || _imageDisplaySize.Width <= 0 || _imageDisplaySize.Height <= 0) return;
        
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
        
        if (startLength < 1) startLength = 1;
        
        var halfNewSize = Math.Clamp(
            _dragStartCropArea.Width / 2 * (currentLength / startLength),
            MinCropSize / 2,
            MaxCropSize / 2
        );
        
        halfNewSize = Math.Min(halfNewSize, Math.Min(centerX - _imageDisplayOffset.X, _imageDisplayOffset.X + _imageDisplaySize.Width - centerX));
        halfNewSize = Math.Min(halfNewSize, Math.Min(centerY - _imageDisplayOffset.Y, _imageDisplayOffset.Y + _imageDisplaySize.Height - centerY));
        
        // Ensure minimum size
        halfNewSize = Math.Max(halfNewSize, MinCropSize / 2);
        
        _cropArea = new Rect(centerX - halfNewSize, centerY - halfNewSize, halfNewSize * 2, halfNewSize * 2);
    }

    private void EndDrag()
    {
        _isDragging = false;
        
        // Update preview when drag ends for better performance
        if (_isPreviewUpdatePending)
        {
            UpdatePreview();
            _isPreviewUpdatePending = false;
        }
        
        // Pointer capture is released automatically when pointer is released
    }

    private void EndResize()
    {
        _isResizing = false;
        _activeHandle = null;
        
        // Update preview when resize ends for better performance
        if (_isPreviewUpdatePending)
        {
            UpdatePreview();
            _isPreviewUpdatePending = false;
        }
        
        // Pointer capture is released automatically when pointer is released
    }

    private void UpdatePreview()
    {
        if (_currentBitmap == null) return;
        
        var croppedImage = CreateCroppedImage();
        if (croppedImage != null)
        {
            var tempPath = SaveBitmapToTempFile(croppedImage);
            PreviewImage.Source = new Bitmap(tempPath);
        }
    }

    private string SaveBitmapToTempFile(Bitmap bitmap)
    {
        try
        {
            if (string.IsNullOrEmpty(_tempPreviewPath))
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "SchoolOrganizer", "CropPreview");
                System.IO.Directory.CreateDirectory(tempDir);
                _tempPreviewPath = Path.Combine(tempDir, "crop_preview.png");
            }

            bitmap.Save(_tempPreviewPath);
            return _tempPreviewPath;
        }
        catch
        {
            return string.Empty;
        }
    }

    private Bitmap? CreateCroppedImage()
    {
        if (_currentBitmap == null) return null;

        // Use full resolution bitmap if available, otherwise fall back to display bitmap
        var sourceBitmap = _fullResolutionBitmap ?? _currentBitmap;

        var scaleX = sourceBitmap.PixelSize.Width / _imageDisplaySize.Width;
        var scaleY = sourceBitmap.PixelSize.Height / _imageDisplaySize.Height;

        var cropX = (_cropArea.X - _imageDisplayOffset.X) * scaleX;
        var cropY = (_cropArea.Y - _imageDisplayOffset.Y) * scaleY;
        var cropWidth = _cropArea.Width * scaleX;
        var cropHeight = _cropArea.Height * scaleY;

        // Use a consistent high-quality output size for all profile images
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
                var sourceRect = new Rect(cropX, cropY, cropWidth, cropHeight);
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

    private void RotateImage(double degrees)
    {
        try
        {
            Log.Information("ConsolidatedImageCropWindow.RotateImage called - degrees: {Degrees}, _currentBitmap: {HasCurrentBitmap}, _fullResolutionBitmap: {HasFullBitmap}", 
                degrees, _currentBitmap != null, _fullResolutionBitmap != null);

            if (_currentBitmap == null || _fullResolutionBitmap == null) 
            {
                Log.Warning("ConsolidatedImageCropWindow.RotateImage - Missing bitmaps, returning");
                return;
            }

            _rotationAngle += degrees;
            
            // Normalize rotation angle to -180 to 180 range
            while (_rotationAngle <= -180) _rotationAngle += 360;
            while (_rotationAngle > 180) _rotationAngle -= 360;

            Log.Information("ConsolidatedImageCropWindow.RotateImage - New rotation angle: {RotationAngle}", _rotationAngle);

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
                    Log.Error("ConsolidatedImageCropWindow.RotateImage - Failed to rotate display bitmap");
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
                    Log.Error("ConsolidatedImageCropWindow.RotateImage - Failed to rotate full resolution bitmap");
                    throw new InvalidOperationException("Failed to rotate full resolution bitmap");
                }
            }

            // Update the main image display
            if (_currentBitmap != null)
            {
                MainImage.Source = _currentBitmap;
            }

            // Update crop area and preview
            InitializeCropArea();
            UpdatePreview();
            
            // Reset drag state to prevent issues with stale coordinates
            _isDragging = false;
            _isResizing = false;

            Log.Information("ConsolidatedImageCropWindow.RotateImage completed successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in ConsolidatedImageCropWindow.RotateImage: {Message}", ex.Message);
            // Reset rotation angle on error
            _rotationAngle -= degrees;
            throw;
        }
    }

    private async Task LoadImageGallery()
    {
        if (_imageGalleryGrid == null) return;
        
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _imageGalleryGrid.Children.Clear();
            _imageGalleryGrid.RowDefinitions.Clear();

            // Load recent images from temp directory
            var tempDir = Path.Combine(Path.GetTempPath(), "SchoolOrganizer", "CropPreview");
            if (System.IO.Directory.Exists(tempDir))
            {
                var imageFiles = System.IO.Directory.GetFiles(tempDir, "*.png")
                    .OrderByDescending(File.GetLastWriteTime)
                    .Take(6)
                    .ToArray();
                
                for (int i = 0; i < imageFiles.Length; i++)
                {
                    int row = i / 2;
                    int col = i % 2;
                    
                    while (_imageGalleryGrid.RowDefinitions.Count <= row)
                        _imageGalleryGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    
                    var border = CreateThumbnailBorder(imageFiles[i]);
                    Grid.SetRow(border, row);
                    Grid.SetColumn(border, col);
                    _imageGalleryGrid.Children.Add(border);
                }
            }
        });
    }

    private Border CreateThumbnailBorder(string imagePath)
    {
        var border = new Border
        {
            Width = ThumbSize,
            Height = ThumbSize,
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            BorderBrush = new SolidColorBrush(Colors.Black),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromRgb(232, 244, 248)),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        
        try
        {
            var image = new Image
            {
                Source = new Bitmap(imagePath),
                Stretch = Stretch.UniformToFill
            };
            border.Child = image;
        }
        catch
        {
            border.Child = new TextBlock
            {
                Text = "?",
                FontSize = 20,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
        }
        
        border.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            {
                LoadImageFromPath(imagePath);
                e.Handled = true;
            }
        };
        
        return border;
    }

    private async void LoadImageFromPath(string imagePath)
    {
        try
        {
            using var fileStream = File.OpenRead(imagePath);
            var bitmap = LoadBitmapWithCorrectOrientation(fileStream, imagePath);

            // Keep full resolution bitmap for high-quality final crop
            _fullResolutionBitmap?.Dispose();
            _fullResolutionBitmap = bitmap;

            // Create a resized version for display if needed
            var displayBitmap = ResizeImageIfNeeded(bitmap);
            await LoadBitmap(displayBitmap);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading image from path: {ex.Message}");
        }
    }

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
                var directories = ImageMetadataReader.ReadMetadata(filePath);
                var exifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

                if (exifSubIfdDirectory != null && exifSubIfdDirectory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientationValue))
                {
                    orientation = orientationValue;
                    Log.Information("EXIF Orientation detected: {Orientation} from file: {FilePath}", orientation, filePath);
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

    private void UpdatePreviewState(bool showActions, bool resetEnabled, bool saveEnabled)
    {
        ActionButtonsPanel.IsVisible = showActions;
        ResetButton.IsEnabled = resetEnabled;
        SaveButton.IsEnabled = saveEnabled;
        RotateLeftButton.IsEnabled = showActions && _currentBitmap != null;
        RotateRightButton.IsEnabled = showActions && _currentBitmap != null;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        ResetImageState();
    }

    private void OnRotateLeftClick(object? sender, RoutedEventArgs e)
    {
        RotateImage(-90);
    }

    private void OnRotateRightClick(object? sender, RoutedEventArgs e)
    {
        RotateImage(90);
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (_currentBitmap != null)
        {
            InitializeCropArea();
            UpdatePreview();
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_currentBitmap == null) return;

        try
        {
            var croppedImage = CreateCroppedImage();
            if (croppedImage == null) return;

            var savePath = await GetSavePath();
            if (string.IsNullOrEmpty(savePath)) return;

            using var stream = File.Create(savePath);
            croppedImage.Save(stream);
            
            SavedImagePath = savePath;
            SavedCropSettings = GetCropSettingsJson();
            Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving image: {ex.Message}");
        }
    }

    private string GetCropSettingsJson()
    {
        var settings = new
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
        
        return System.Text.Json.JsonSerializer.Serialize(settings);
    }

    private async Task<string?> GetSavePath()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return null;

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

    private void ResetImageState()
    {
        _currentBitmap?.Dispose();
        _currentBitmap = null;
        
        MainImage.Source = null;
        MainImage.IsVisible = false;
        BackgroundPattern.IsVisible = true;
        CropOverlay.IsVisible = false;
        
        UpdatePreviewState(false, false, false);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private static double CalculateAngle(Point center, Point point)
    {
        return Math.Atan2(point.Y - center.Y, point.X - center.X) * 180 / Math.PI;
    }

    private static Rect SnapToPixels(Rect rect)
    {
        var left = Math.Floor(rect.X);
        var top = Math.Floor(rect.Y);
        var right = Math.Ceiling(rect.Right);
        var bottom = Math.Ceiling(rect.Bottom);
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    protected override void OnClosed(EventArgs e)
    {
        _currentBitmap?.Dispose();
        
        if (!string.IsNullOrEmpty(_tempPreviewPath) && File.Exists(_tempPreviewPath))
        {
            try { File.Delete(_tempPreviewPath); }
            catch { }
        }
        
        base.OnClosed(e);
    }
}
