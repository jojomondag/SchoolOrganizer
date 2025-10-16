using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Animation;
using Avalonia.Input;
using Avalonia.Controls.Shapes;
using Serilog;

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

    public static readonly StyledProperty<bool> ShowInitialsProperty =
        AvaloniaProperty.Register<ProfileImage, bool>(nameof(ShowInitials), false);

    public static readonly StyledProperty<string> InitialsProperty =
        AvaloniaProperty.Register<ProfileImage, string>(nameof(Initials), "");

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

    public bool ShowInitials
    {
        get => GetValue(ShowInitialsProperty);
        set => SetValue(ShowInitialsProperty, value);
    }

    public string Initials
    {
        get => GetValue(InitialsProperty);
        set => SetValue(InitialsProperty, value);
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

    public new static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<ProfileImage, CornerRadius>(nameof(CornerRadius));

    public new CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public event EventHandler? ImageClicked;

    public ProfileImage()
    {
        InitializeComponent();
        
        // Log only in debug mode to reduce log noise
        Log.Debug("ProfileImage: Constructor called - Width: {Width}, Height: {Height}, BorderThickness: {BorderThickness}", Width, Height, BorderThickness);
        
        // Update layout when size changes
        WidthProperty.Changed.AddClassHandler<ProfileImage>((control, e) =>
        {
            control.UpdateCornerRadius();
            control.UpdateLayout();
        });
        
        HeightProperty.Changed.AddClassHandler<ProfileImage>((control, e) =>
        {
            control.UpdateCornerRadius();
            control.UpdateLayout();
        });
        
        // Update IsImageMissing when ImagePath changes
        ImagePathProperty.Changed.AddClassHandler<ProfileImage>((control, e) =>
        {
            control.IsImageMissing = string.IsNullOrWhiteSpace(control.ImagePath);
            control.UpdateVisibility();
        });
        
        // Update visibility when properties change
        ShowInitialsProperty.Changed.AddClassHandler<ProfileImage>((control, e) =>
        {
            control.UpdateVisibility();
        });
        
        ShowPlusIconProperty.Changed.AddClassHandler<ProfileImage>((control, e) =>
        {
            control.UpdateVisibility();
        });
        
        // Add hover event handlers
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Log.Debug("ProfileImage: OnLoaded called");
        
        if (this.FindControl<Border>("ProfileImageBorderElement") is { } profileImageBorder)
        {
            Log.Debug("ProfileImage: ProfileImageBorderElement found, attaching event handlers");
            profileImageBorder.PointerEntered += OnProfileImagePointerEntered;
            profileImageBorder.PointerExited += OnProfileImagePointerExited;
        }
        else
        {
            Log.Warning("ProfileImage: ProfileImageBorderElement not found");
        }
        
        // Set the corner radius and update visibility
        UpdateCornerRadius();
        UpdateLayout();
    }

    private void OnImageClick(object? sender, RoutedEventArgs e)
    {
        if (IsClickable)
        {
            ImageClicked?.Invoke(this, EventArgs.Empty);
        }
    }
    
    private void OnProfileImagePointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        Log.Debug("ProfileImage: OnProfileImagePointerEntered called, IsClickable: {IsClickable}", IsClickable);
        
        if (IsClickable)
        {
            if (this.FindControl<Border>("ProfileImageBorderElement") is { } profileImageBorder)
            {
                // Apply enhanced shadow on hover
                profileImageBorder.BoxShadow = new BoxShadow 
                { 
                    OffsetX = 0, 
                    OffsetY = 4, 
                    BlurRadius = 16, 
                    SpreadRadius = 0, 
                    Color = new Color(255, 0, 0, 0) 
                };
                Log.Debug("ProfileImage: Shadow enhanced for hover effect");
            }
        }
    }
    
    private void OnProfileImagePointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        Log.Debug("ProfileImage: OnProfileImagePointerExited called, IsClickable: {IsClickable}", IsClickable);
        
        if (IsClickable)
        {
            if (this.FindControl<Border>("ProfileImageBorderElement") is { } profileImageBorder)
            {
                // Restore normal shadow
                profileImageBorder.BoxShadow = new BoxShadow 
                { 
                    OffsetX = 0, 
                    OffsetY = 2, 
                    BlurRadius = 8, 
                    SpreadRadius = 0, 
                    Color = new Color(255, 0, 0, 0) 
                };
                Log.Debug("ProfileImage: Shadow restored to normal");
            }
        }
    }

    private void UpdateCornerRadius()
    {
        var radius = Math.Max(Width, Height) / 2;
        CornerRadius = new CornerRadius(radius);
    }

    private new void UpdateLayout()
    {
        Log.Debug("ProfileImage: UpdateLayout called - Width: {Width}, Height: {Height}, CornerRadius: {CornerRadius}", Width, Height, CornerRadius);
        
        // Set corner radius
        if (this.FindControl<Border>("ProfileImageBorderElement") is { } border)
        {
            border.CornerRadius = CornerRadius;
            Log.Debug("ProfileImage: CornerRadius set to {CornerRadius}", CornerRadius);
            Log.Debug("ProfileImage: Border properties - BorderBrush: {BorderBrush}, BorderThickness: {BorderThickness}", border.BorderBrush, border.BorderThickness);
        }
        else
        {
            Log.Warning("ProfileImage: ProfileImageBorderElement not found in UpdateLayout");
        }
        
        // Update font sizes and icon sizes
        if (this.FindControl<TextBlock>("InitialsText") is { } initialsText)
        {
            initialsText.FontSize = Math.Max(8, Width * 0.3);
        }
        
        if (this.FindControl<TextBlock>("PlaceholderText") is { } placeholderText)
        {
            placeholderText.FontSize = Math.Max(8, PlaceholderFontSize);
        }
        
        if (this.FindControl<Viewbox>("PlusIconViewbox") is { } plusIconViewbox)
        {
            var iconSize = Math.Max(16, Width * 0.4);
            plusIconViewbox.Width = iconSize;
            plusIconViewbox.Height = iconSize;
        }
        
        // Update visibility based on properties
        UpdateVisibility();
    }
    
    private void UpdateVisibility()
    {
        // Update profile image visibility
        if (this.FindControl<Ellipse>("ProfileImageEllipse") is { } imageEllipse)
        {
            imageEllipse.IsVisible = !IsImageMissing && !ShowInitials && !ShowPlusIcon;
        }
        
        // Update placeholder text visibility
        if (this.FindControl<Ellipse>("PlaceholderEllipse") is { } placeholderEllipse)
        {
            placeholderEllipse.IsVisible = IsImageMissing && !ShowInitials && !ShowPlusIcon;
        }
        
        // Update initials visibility
        if (this.FindControl<Ellipse>("InitialsEllipse") is { } initialsEllipse)
        {
            initialsEllipse.IsVisible = ShowInitials;
        }
        
        // Update plus icon visibility
        if (this.FindControl<Ellipse>("PlusIconEllipse") is { } plusIconEllipse)
        {
            plusIconEllipse.IsVisible = ShowPlusIcon;
        }
    }

}
