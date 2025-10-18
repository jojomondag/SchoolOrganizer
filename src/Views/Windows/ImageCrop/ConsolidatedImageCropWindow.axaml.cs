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

namespace SchoolOrganizer.Src.Views.Windows.ImageCrop;

public partial class ConsolidatedImageCropWindow : Window
{
    private const double MinCropSize = 50;
    private const double MaxCropSize = 400;
    private const int ThumbSize = 90;
    
    private Bitmap? _currentBitmap;
    private bool _isDragging;
    private bool _isResizing;
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
            
            var bitmap = new Bitmap(memoryStream);
            await LoadBitmap(bitmap);
            await LoadImageGallery();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading image: {ex.Message}");
        }
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
        UpdatePreview();
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
        UpdatePreview();
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
        
        if (startLength < 1) startLength = 1;
        
        var halfNewSize = Math.Clamp(
            _dragStartCropArea.Width / 2 * (currentLength / startLength),
            MinCropSize / 2,
            MaxCropSize / 2
        );
        
        halfNewSize = Math.Min(halfNewSize, Math.Min(centerX - _imageDisplayOffset.X, _imageDisplayOffset.X + _imageDisplaySize.Width - centerX));
        halfNewSize = Math.Min(halfNewSize, Math.Min(centerY - _imageDisplayOffset.Y, _imageDisplayOffset.Y + _imageDisplaySize.Height - centerY));
        
        _cropArea = new Rect(centerX - halfNewSize, centerY - halfNewSize, halfNewSize * 2, halfNewSize * 2);
    }

    private void EndDrag()
    {
        _isDragging = false;
        // Pointer capture is released automatically when pointer is released
    }

    private void EndResize()
    {
        _isResizing = false;
        _activeHandle = null;
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
                Directory.CreateDirectory(tempDir);
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

        var scaleX = _currentBitmap.PixelSize.Width / _imageDisplaySize.Width;
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
                var sourceX = Math.Max(0, centerX - outputSize / 2.0);
                var sourceY = Math.Max(0, centerY - outputSize / 2.0);
                var sourceWidth = Math.Min(outputSize, _currentBitmap.PixelSize.Width - sourceX);
                var sourceHeight = Math.Min(outputSize, _currentBitmap.PixelSize.Height - sourceY);
                
                var sourceRect = new Rect(sourceX, sourceY, sourceWidth, sourceHeight);
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

    private async Task LoadImageGallery()
    {
        if (_imageGalleryGrid == null) return;
        
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _imageGalleryGrid.Children.Clear();
            _imageGalleryGrid.RowDefinitions.Clear();
            
            // Load recent images from temp directory
            var tempDir = Path.Combine(Path.GetTempPath(), "SchoolOrganizer", "CropPreview");
            if (Directory.Exists(tempDir))
            {
                var imageFiles = Directory.GetFiles(tempDir, "*.png")
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
            var bitmap = new Bitmap(fileStream);
            await LoadBitmap(bitmap);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading image from path: {ex.Message}");
        }
    }

    private void UpdatePreviewState(bool showActions, bool resetEnabled, bool saveEnabled)
    {
        ActionButtonsPanel.IsVisible = showActions;
        ResetButton.IsEnabled = resetEnabled;
        SaveButton.IsEnabled = saveEnabled;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        ResetImageState();
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
