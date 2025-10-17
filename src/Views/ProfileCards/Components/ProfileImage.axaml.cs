using System;
using System.IO;
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

    private async void OnImageClick(object? sender, RoutedEventArgs e)
    {
        if (IsClickable)
        {
            try
            {
                // Find the parent window
                var parentWindow = this.FindAncestorOfType<Window>();
                if (parentWindow != null)
                {
                    Log.Information("Opening image crop window with existing settings...");
                    Log.Information("Current OriginalImagePath: {OriginalPath}", OriginalImagePath ?? "null");
                    Log.Information("Current CropSettings: {CropSettings}", CropSettings ?? "null");
                    
                    // Use a dummy student ID for the crop window
                    var studentId = 1; // This could be made configurable if needed
                    
                    // Open the crop window with existing image and crop settings
                    var (imagePath, cropSettings, originalImagePath) = await ImageCropWindow.ShowForStudentAsync(
                        parentWindow, 
                        studentId, 
                        OriginalImagePath, 
                        CropSettings);
                    
                    // Log the returned values
                    Log.Information("Crop window returned - ImagePath: {ImagePath}, CropSettings: {CropSettings}, OriginalPath: {OriginalPath}", 
                        imagePath ?? "null", cropSettings ?? "null", originalImagePath ?? "null");
                    
                    // If a new image was selected and saved, update the properties
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        Log.Information("New image selected: {ImagePath}", imagePath);
                        Log.Information("File exists: {Exists}", File.Exists(imagePath));
                        System.Diagnostics.Debug.WriteLine($"Main ProfileImage updating from '{ImagePath}' to '{imagePath}'");
                        
                        // Clear the current image first to force refresh
                        var oldPath = ImagePath;
                        ImagePath = string.Empty;
                        
                        // Use Dispatcher to ensure the UI updates on the UI thread
                        Dispatcher.UIThread.Post(() =>
                        {
                            // Clear the cache for this path to ensure fresh image loading
                            UniversalImageConverter.ClearCache(imagePath);
                            
                            ImagePath = imagePath;
                            System.Diagnostics.Debug.WriteLine($"Main ProfileImage.ImagePath is now: {ImagePath}");
                            
                            // Property change notification is automatic when setting ImagePath
                        }, DispatcherPriority.Render);
                        
                        // Store the original image path and crop settings
                        if (!string.IsNullOrEmpty(originalImagePath))
                        {
                            OriginalImagePath = originalImagePath;
                            Log.Information("Stored OriginalImagePath: {OriginalPath}", originalImagePath);
                        }
                        
                        if (!string.IsNullOrEmpty(cropSettings))
                        {
                            CropSettings = cropSettings;
                            Log.Information("Stored CropSettings: {CropSettings}", cropSettings);
                        }
                        
                        // Force a refresh of the image display
                        ForceImageRefresh();
                        
                        Log.Information("Profile image updated from '{OldPath}' to '{NewPath}' with crop settings", oldPath, imagePath);
                    }
                    else
                    {
                        Log.Information("No image selected or crop window cancelled");
                    }
                }
                else
                {
                    Log.Warning("Could not find parent window for crop dialog");
                }
                
                // Still fire the event for any other listeners
                ImageClicked?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error opening image crop window");
            }
        }
    }

    /// <summary>
    /// Forces a refresh of the image by temporarily clearing and resetting the ImagePath
    /// </summary>
    public void ForceImageRefresh()
    {
        if (!string.IsNullOrEmpty(ImagePath))
        {
            System.Diagnostics.Debug.WriteLine($"ProfileImage.ForceImageRefresh called for: {ImagePath}");
            
            // Force a property change notification to refresh the image
            var currentPath = ImagePath;
            ImagePath = string.Empty;
            
            // Use Dispatcher to ensure the UI updates
            Dispatcher.UIThread.Post(() =>
            {
                // Clear the cache for this path to ensure fresh image loading
                UniversalImageConverter.ClearCache(currentPath);
                
                ImagePath = currentPath;
                System.Diagnostics.Debug.WriteLine($"ProfileImage.ForceImageRefresh set ImagePath to: {ImagePath}");
                
                // Property change notification is automatic when setting ImagePath
            }, DispatcherPriority.Render);
        }
    }
}