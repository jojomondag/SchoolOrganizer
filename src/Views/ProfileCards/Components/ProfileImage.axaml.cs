using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Serilog;
using SchoolOrganizer.Src.Views.Windows.ImageCrop;
using Avalonia.VisualTree;
using Avalonia.Threading;
using SchoolOrganizer.Src.Converters;

namespace SchoolOrganizer.Src.Views.ProfileCards.Components;

public partial class ProfileImage : UserControl
{
    private static bool _isAnyImageSelectionInProgress = false;
    private bool _isProcessingImageSelection = false;
    private static DateTime _lastImageClickTime = DateTime.MinValue;
    private const int MinClickIntervalMs = 1000; // Minimum 1 second between clicks
    
    public static readonly StyledProperty<string> ImagePathProperty =
        AvaloniaProperty.Register<ProfileImage, string>(nameof(ImagePath));

    public new static readonly StyledProperty<double> WidthProperty =
        AvaloniaProperty.Register<ProfileImage, double>(nameof(Width), 100.0);

    public new static readonly StyledProperty<double> HeightProperty =
        AvaloniaProperty.Register<ProfileImage, double>(nameof(Height), 100.0);

    public new static readonly StyledProperty<Thickness> BorderThicknessProperty =
        AvaloniaProperty.Register<ProfileImage, Thickness>(nameof(BorderThickness), new Thickness(2));

    public static readonly StyledProperty<bool> IsImageMissingProperty =
        AvaloniaProperty.Register<ProfileImage, bool>(nameof(IsImageMissing));

    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<ProfileImage, string>(nameof(PlaceholderTextValue), "+");

    public static readonly StyledProperty<double> PlaceholderFontSizeProperty =
        AvaloniaProperty.Register<ProfileImage, double>(nameof(PlaceholderFontSize), 28);


    public static readonly StyledProperty<bool> IsClickableProperty =
        AvaloniaProperty.Register<ProfileImage, bool>(nameof(IsClickable), true);

    public static readonly StyledProperty<string> OriginalImagePathProperty =
        AvaloniaProperty.Register<ProfileImage, string>(nameof(OriginalImagePath));

    public static readonly StyledProperty<string> CropSettingsProperty =
        AvaloniaProperty.Register<ProfileImage, string>(nameof(CropSettings));


    public new static readonly StyledProperty<Cursor> CursorProperty =
        AvaloniaProperty.Register<ProfileImage, Cursor>(nameof(Cursor), Cursor.Parse("Hand"));

    public string ImagePath
    {
        get => GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public new double Width
    {
        get => GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    public new double Height
    {
        get => GetValue(HeightProperty);
        set => SetValue(HeightProperty, value);
    }

    public new Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public bool IsImageMissing
    {
        get => GetValue(IsImageMissingProperty);
        set => SetValue(IsImageMissingProperty, value);
    }

    public string PlaceholderTextValue
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public double PlaceholderFontSize
    {
        get => GetValue(PlaceholderFontSizeProperty);
        set => SetValue(PlaceholderFontSizeProperty, value);
    }


    public bool IsClickable
    {
        get => GetValue(IsClickableProperty);
        set => SetValue(IsClickableProperty, value);
    }

    public string OriginalImagePath
    {
        get => GetValue(OriginalImagePathProperty);
        set => SetValue(OriginalImagePathProperty, value);
    }

    public string CropSettings
    {
        get => GetValue(CropSettingsProperty);
        set => SetValue(CropSettingsProperty, value);
    }


    public new Cursor Cursor
    {
        get => GetValue(CursorProperty);
        set => SetValue(CursorProperty, value);
    }

    public event EventHandler? ImageClicked;

    public ProfileImage()
    {
        InitializeComponent();
        
        // Log when ProfileImage is created
        Log.Information("ProfileImage created - Name: {Name}, IsClickable: {IsClickable}", this.Name ?? "unnamed", IsClickable);
        
        // Log when IsClickable property changes
        IsClickableProperty.Changed.AddClassHandler<ProfileImage>((control, e) =>
        {
            Log.Information("ProfileImage IsClickable changed to: {IsClickable}", control.IsClickable);
        });
        
        
        // Update IsImageMissing when ImagePath changes - use more efficient approach
        ImagePathProperty.Changed.AddClassHandler<ProfileImage>((control, e) =>
        {
            var newValue = string.IsNullOrWhiteSpace(control.ImagePath);
            if (control.IsImageMissing != newValue)
            {
                control.IsImageMissing = newValue;
            }
        });

        // Add hover event handlers to the ProfileImage component
        this.PointerEntered += OnProfileImagePointerEntered;
        this.PointerExited += OnProfileImagePointerExited;
    }

    private void OnProfileImagePointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (this.FindControl<Border>("ProfileImageBorder") is { } border)
        {
            border.BoxShadow = new BoxShadows(
                new BoxShadow { Blur = 12, OffsetY = 4, OffsetX = 0, Color = Color.Parse("#CC000000") }
            );
            border.BorderBrush = new SolidColorBrush(Color.Parse("#000000"));
            // Don't change BorderThickness - keep it the same
        }
    }

    private void OnProfileImagePointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (this.FindControl<Border>("ProfileImageBorder") is { } border)
        {
            border.BoxShadow = new BoxShadows(); // Clear shadow
            border.BorderBrush = new SolidColorBrush(Color.Parse("#000000"));
            // Don't change BorderThickness - keep it the same
        }
    }

    private void OnImagePointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // CRITICAL: Stop the PointerPressed event from bubbling to the parent CardBorder
        // The CardBorder handles PointerPressed to select the card, but we don't want
        // that to happen when clicking the profile image
        e.Handled = true;
        Log.Information("ProfileImage.OnImagePointerPressed - Event handled to prevent card selection");
    }

    private async void OnImageClick(object? sender, TappedEventArgs e)
    {
        // IMPORTANT: Mark event as handled immediately to prevent it from bubbling up to the card
        // This prevents the card selection when clicking the profile image
        e.Handled = true;

        Log.Information("ProfileImage.OnImageClick called - IsClickable: {IsClickable}, Processing: {Processing}, Global: {Global}, Name: {Name}, Parent: {ParentType}, Sender: {SenderType}",
            IsClickable, _isProcessingImageSelection, _isAnyImageSelectionInProgress, this.Name ?? "unnamed", this.Parent?.GetType().Name ?? "null", sender?.GetType().Name ?? "null");

        System.Diagnostics.Debug.WriteLine($"ProfileImage.OnImageClick called - IsClickable: {IsClickable}, Processing: {_isProcessingImageSelection}, Global: {_isAnyImageSelectionInProgress}");


        // Check for rapid successive clicks
        var currentTime = DateTime.Now;
        if (currentTime - _lastImageClickTime < TimeSpan.FromMilliseconds(MinClickIntervalMs))
        {
            Log.Warning("ProfileImage.OnImageClick - Click too soon after previous click, ignoring");
            return;
        }
        _lastImageClickTime = currentTime;

        if (!IsClickable || _isProcessingImageSelection || _isAnyImageSelectionInProgress)
        {
            Log.Warning("ProfileImage.OnImageClick - Early return due to guard conditions: IsClickable={IsClickable}, Processing={Processing}, Global={Global}",
                IsClickable, _isProcessingImageSelection, _isAnyImageSelectionInProgress);
            System.Diagnostics.Debug.WriteLine("ProfileImage.OnImageClick - Early return due to guard conditions");
            return;
        }

        Log.Information("ProfileImage.OnImageClick - Starting image selection process");
        System.Diagnostics.Debug.WriteLine("ProfileImage.OnImageClick - Starting image selection process");
        _isProcessingImageSelection = true;
        _isAnyImageSelectionInProgress = true;
        try
        {
            // Find the parent window
            var parentWindow = this.FindAncestorOfType<Window>();
            Log.Information("ProfileImage.OnImageClick - Parent window found: {ParentWindowType}", parentWindow?.GetType().Name ?? "null");
            
            string? imagePath = null;
            string? cropSettings = null;
            string? originalImagePath = null;
            
            if (parentWindow != null)
            {
                // Use the full-featured crop window with existing settings for editing
                Log.Information("Opening image crop window with existing settings...");
                Log.Information("Current OriginalImagePath: {OriginalPath}", OriginalImagePath ?? "null");
                Log.Information("Current CropSettings: {CropSettings}", CropSettings ?? "null");
                
                // Use a dummy student ID for the crop window
                var studentId = 1; // This could be made configurable if needed
                
                // Open the crop window with existing image and crop settings
                var result = await ImageCropWindow.ShowForStudentAsync(
                    parentWindow, 
                    studentId, 
                    OriginalImagePath, 
                    CropSettings);
                
                imagePath = result.imagePath;
                cropSettings = result.cropSettings;
                originalImagePath = result.originalImagePath;
                
                // Log the returned values
                Log.Information("Crop window returned - ImagePath: {ImagePath}, CropSettings: {CropSettings}, OriginalPath: {OriginalPath}", 
                    imagePath ?? "null", cropSettings ?? "null", originalImagePath ?? "null");
                
                // If a new image was selected and saved, update the properties
                if (!string.IsNullOrEmpty(imagePath))
                {
                    Log.Information("ProfileImage.OnImageClick - New image selected: {ImagePath}", imagePath);
                    Log.Information("ProfileImage.OnImageClick - File exists: {Exists}", File.Exists(imagePath));
                    System.Diagnostics.Debug.WriteLine($"Main ProfileImage updating from '{ImagePath}' to '{imagePath}'");
                    
                    // Clear the cache for this path to ensure fresh image loading
                    UniversalImageConverter.ClearCache(imagePath);
                    Log.Information("ProfileImage.OnImageClick - Cleared image converter cache for: {ImagePath}", imagePath);
                    
                    // Update the image path in a single operation to avoid multiple binding updates
                    Log.Information("ProfileImage.OnImageClick - Updating ImagePath from '{OldPath}' to '{NewPath}'", ImagePath, imagePath);
                    ImagePath = imagePath;
                    Log.Information("ProfileImage.OnImageClick - ImagePath updated successfully to: {ImagePath}", ImagePath);
                    System.Diagnostics.Debug.WriteLine($"Main ProfileImage.ImagePath is now: {ImagePath}");
                    
                    // Store the original image path and crop settings
                    if (!string.IsNullOrEmpty(originalImagePath))
                    {
                        OriginalImagePath = originalImagePath;
                        Log.Information("ProfileImage.OnImageClick - Stored OriginalImagePath: {OriginalPath}", originalImagePath);
                    }
                    
                    if (!string.IsNullOrEmpty(cropSettings))
                    {
                        CropSettings = cropSettings;
                        Log.Information("ProfileImage.OnImageClick - Stored CropSettings: {CropSettings}", cropSettings);
                    }
                    
                    Log.Information("ProfileImage.OnImageClick - Profile image update completed successfully");
                }
                else
                {
                    Log.Information("ProfileImage.OnImageClick - No image selected or crop window cancelled");
                }
            }
            else
            {
                Log.Warning("Could not find parent window for crop dialog");
            }
            
            // Don't fire the ImageClicked event since we already handled the image selection here
            // Firing this event would cause a second crop window to open via the event chain
            Log.Information("ProfileImage.OnImageClick - Skipping ImageClicked event to prevent duplicate crop window");
            Log.Information("ProfileImage.OnImageClick - Event handled successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProfileImage.OnImageClick - Error opening image crop window");
        }
        finally
        {
            Log.Information("ProfileImage.OnImageClick - Resetting processing flags");
            _isProcessingImageSelection = false;
            // Only reset the global flag after a longer delay to prevent race conditions
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(500); // Longer delay to prevent race conditions
                _isAnyImageSelectionInProgress = false;
                Log.Information("ProfileImage.OnImageClick - Processing completed");
            });
        }
    }

    /// <summary>
    /// Forces a refresh of the image by temporarily clearing and resetting the ImagePath
    /// </summary>
    public void ForceImageRefresh()
    {
        // Don't refresh if we're currently processing an image selection
        if (_isProcessingImageSelection || _isAnyImageSelectionInProgress || string.IsNullOrEmpty(ImagePath))
            return;

        System.Diagnostics.Debug.WriteLine($"ProfileImage.ForceImageRefresh called for: {ImagePath}");
        
        // Clear the cache for this path to ensure fresh image loading
        UniversalImageConverter.ClearCache(ImagePath);
        
        // Force a property change notification to refresh the image
        var currentPath = ImagePath;
        ImagePath = string.Empty;
        ImagePath = currentPath;
        
        System.Diagnostics.Debug.WriteLine($"ProfileImage.ForceImageRefresh set ImagePath to: {ImagePath}");
    }
}