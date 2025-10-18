using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Serilog;
using SchoolOrganizer.Src.Converters;

namespace SchoolOrganizer.Src.Views.ProfileCards.Components;

public partial class ProfileImage : UserControl
{
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

    private void OnImageClick(object? sender, TappedEventArgs e)
    {
        // IMPORTANT: Mark event as handled immediately to prevent it from bubbling up to the card
        // This prevents the card selection when clicking the profile image
        e.Handled = true;

        Log.Information("ProfileImage.OnImageClick called - IsClickable: {IsClickable}, Name: {Name}",
            IsClickable, this.Name ?? "unnamed");

        // Check for rapid successive clicks
        var currentTime = DateTime.Now;
        if (currentTime - _lastImageClickTime < TimeSpan.FromMilliseconds(MinClickIntervalMs))
        {
            Log.Warning("ProfileImage.OnImageClick - Click too soon after previous click, ignoring");
            return;
        }
        _lastImageClickTime = currentTime;

        if (!IsClickable)
        {
            Log.Warning("ProfileImage.OnImageClick - Not clickable, ignoring");
            return;
        }

        Log.Information("ProfileImage.OnImageClick - Firing ImageClicked event");

        // Fire the ImageClicked event for the parent (BaseProfileCard) to handle
        // The parent will use StudentCoordinatorService to update the Student model properly
        ImageClicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Forces a refresh of the image by temporarily clearing and resetting the ImagePath
    /// </summary>
    public void ForceImageRefresh()
    {
        if (string.IsNullOrEmpty(ImagePath))
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