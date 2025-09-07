using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchoolOrganizer.Models;

namespace SchoolOrganizer.Views.ProfileCard;

public partial class ProfileCard : UserControl
{
    public event EventHandler<Student>? ImageClicked;

    public static readonly StyledProperty<int> StudentCountProperty =
        AvaloniaProperty.Register<ProfileCard, int>(nameof(StudentCount), 16);

    public int StudentCount
    {
        get => GetValue(StudentCountProperty);
        set => SetValue(StudentCountProperty, value);
    }

    public ProfileCard()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == StudentCountProperty)
        {
            UpdateCardSize(StudentCount);
        }
    }

    private void UpdateCardSize(int count)
    {
        if (ProfileImageBorder == null) return;

        // Update image size based on student count
        var imageSize = count switch
        {
            <= 4 => 150.0,   // Expanded
            <= 8 => 120.0,   // Detailed  
            <= 16 => 90.0,   // Standard
            _ => 70.0         // Compact
        };

        var cornerRadius = imageSize / 2;
        var innerSize = imageSize - 6;

        // Update image dimensions
        ProfileImageBorder.Width = imageSize;
        ProfileImageBorder.Height = imageSize;
        ProfileImageBorder.CornerRadius = new CornerRadius(cornerRadius);
        
        ProfileImageGrid.Width = innerSize;
        ProfileImageGrid.Height = innerSize;
        ProfileImageBackground.Width = innerSize;
        ProfileImageBackground.Height = innerSize;
        ProfileImageEllipse.Width = innerSize;
        ProfileImageEllipse.Height = innerSize;

        // Update font sizes
        NameText.FontSize = count switch
        {
            <= 4 => 20.0,    // Expanded
            <= 8 => 18.0,    // Detailed
            <= 16 => 16.0,   // Standard
            _ => 14.0         // Compact
        };

        RoleText.FontSize = count switch
        {
            <= 4 => 16.0,    // Expanded
            <= 8 => 14.0,    // Detailed
            <= 16 => 12.0,   // Standard
            _ => 10.0         // Compact
        };

        SecondaryText.FontSize = count switch
        {
            <= 4 => 14.0,    // Expanded
            <= 8 => 12.0,    // Detailed
            <= 16 => 10.0,   // Standard
            _ => 9.0          // Compact
        };

        // Update visibility
        EmailText.IsVisible = count <= 8 && !string.IsNullOrWhiteSpace(((IPerson?)DataContext)?.Email);
        EnrollmentText.IsVisible = count <= 4;
    }

    private void OnProfileImageClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is Student student)
        {
            ImageClicked?.Invoke(this, student);
        }
    }

    private void OnProfileImagePointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        // Only respond to primary button releases
        try
        {
            if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left)
            {
                if (DataContext is Student student)
                {
                    ImageClicked?.Invoke(this, student);
                    e.Handled = true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ProfileImage pointer handler error: {ex.Message}");
        }
    }
}