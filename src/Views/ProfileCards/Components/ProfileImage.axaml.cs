using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;

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

    public static readonly StyledProperty<bool> ShowPlusIconProperty =
        AvaloniaProperty.Register<ProfileImage, bool>(nameof(ShowPlusIcon), false);

    public static readonly StyledProperty<bool> IsClickableProperty =
        AvaloniaProperty.Register<ProfileImage, bool>(nameof(IsClickable), true);

    public static readonly StyledProperty<string> ToolTipTextProperty =
        AvaloniaProperty.Register<ProfileImage, string>(nameof(ToolTipText), "Click to edit");

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

    public bool ShowPlusIcon
    {
        get => GetValue(ShowPlusIconProperty);
        set => SetValue(ShowPlusIconProperty, value);
    }

    public bool IsClickable
    {
        get => GetValue(IsClickableProperty);
        set => SetValue(IsClickableProperty, value);
    }

    public string ToolTipText
    {
        get => GetValue(ToolTipTextProperty);
        set => SetValue(ToolTipTextProperty, value);
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
            
            // Add debugging to track ImagePath changes
            System.Diagnostics.Debug.WriteLine($"ProfileImage: ImagePath changed to '{control.ImagePath}' (IsImageMissing: {control.IsImageMissing})");
            System.Diagnostics.Debug.WriteLine($"ProfileImage: DataContext is {control.DataContext?.GetType().Name ?? "null"}");
            System.Diagnostics.Debug.WriteLine($"ProfileImage: Parent is {control.Parent?.GetType().Name ?? "null"}");
            
            // Force refresh of the image when path changes
            if (!string.IsNullOrWhiteSpace(control.ImagePath))
            {
                // Avoid infinite loop by not calling ForceImageRefresh here
                // The image will be refreshed automatically by the binding
            }
        });
    }

    private void OnImageClick(object? sender, RoutedEventArgs e)
    {
        if (IsClickable)
        {
            ImageClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Forces a refresh of the image by temporarily clearing and resetting the ImagePath
    /// </summary>
    public void ForceImageRefresh()
    {
        if (!string.IsNullOrWhiteSpace(ImagePath))
        {
            var currentPath = ImagePath;
            ImagePath = "";
            ImagePath = currentPath;
            System.Diagnostics.Debug.WriteLine($"ProfileImage: Forced refresh of image '{currentPath}'");
        }
    }
}