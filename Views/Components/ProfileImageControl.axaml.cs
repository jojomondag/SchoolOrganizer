using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SchoolOrganizer.Views.Components;

public partial class ProfileImageControl : UserControl
{
    public static readonly StyledProperty<string> ImagePathProperty =
        AvaloniaProperty.Register<ProfileImageControl, string>(nameof(ImagePath));

    public static readonly StyledProperty<bool> IsImageMissingProperty =
        AvaloniaProperty.Register<ProfileImageControl, bool>(nameof(IsImageMissing));

    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<ProfileImageControl, string>(nameof(PlaceholderText), "+");

    public static readonly StyledProperty<double> PlaceholderFontSizeProperty =
        AvaloniaProperty.Register<ProfileImageControl, double>(nameof(PlaceholderFontSize), 28);

    public static readonly StyledProperty<string> ToolTipTextProperty =
        AvaloniaProperty.Register<ProfileImageControl, string>(nameof(ToolTipText), "Add photo");

    public static readonly StyledProperty<double> ImageCornerRadiusProperty =
        AvaloniaProperty.Register<ProfileImageControl, double>(nameof(ImageCornerRadius), 50);

    public string ImagePath
    {
        get => GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public bool IsImageMissing
    {
        get => GetValue(IsImageMissingProperty);
        set => SetValue(IsImageMissingProperty, value);
    }

    public string PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public double PlaceholderFontSize
    {
        get => GetValue(PlaceholderFontSizeProperty);
        set => SetValue(PlaceholderFontSizeProperty, value);
    }

    public string ToolTipText
    {
        get => GetValue(ToolTipTextProperty);
        set => SetValue(ToolTipTextProperty, value);
    }

    public double ImageCornerRadius
    {
        get => GetValue(ImageCornerRadiusProperty);
        set => SetValue(ImageCornerRadiusProperty, value);
    }

    public event EventHandler? ImageClicked;

    public ProfileImageControl()
    {
        InitializeComponent();
        
        // Update IsImageMissing when ImagePath changes
        ImagePathProperty.Changed.AddClassHandler<ProfileImageControl>((control, e) =>
        {
            control.IsImageMissing = string.IsNullOrWhiteSpace(control.ImagePath);
        });
    }

    private void OnImageClick(object? sender, RoutedEventArgs e)
    {
        ImageClicked?.Invoke(this, EventArgs.Empty);
    }
}
