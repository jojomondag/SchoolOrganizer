using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchoolOrganizer.Models;

namespace SchoolOrganizer.Views.ProfileCard;

public partial class ProfileCard : UserControl
{
    public event EventHandler<Student>? ImageClicked;

    public static readonly StyledProperty<ProfileCardDisplayConfig> DisplayConfigProperty =
        AvaloniaProperty.Register<ProfileCard, ProfileCardDisplayConfig>(
            nameof(DisplayConfig), 
            ProfileCardDisplayConfig.GetConfig(ProfileCardDisplayLevel.Standard));

    public ProfileCardDisplayConfig DisplayConfig
    {
        get => GetValue(DisplayConfigProperty);
        set => SetValue(DisplayConfigProperty, value);
    }

    public ProfileCard()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == DisplayConfigProperty)
        {
            UpdateCardAppearance(DisplayConfig);
        }
        else if (change.Property == DataContextProperty)
        {
            UpdateImageInteractivity();
        }
    }

    private void UpdateCardAppearance(ProfileCardDisplayConfig config)
    {
        if (ProfileImageBorder == null) return;

        var cornerRadius = config.ImageSize / 2;
        var innerSize = config.ImageSize - 6;

        // Update image dimensions
        ProfileImageBorder.Width = config.ImageSize;
        ProfileImageBorder.Height = config.ImageSize;
        ProfileImageBorder.CornerRadius = new CornerRadius(cornerRadius);
        
        ProfileImageGrid.Width = innerSize;
        ProfileImageGrid.Height = innerSize;
        ProfileImageBackground.Width = innerSize;
        ProfileImageBackground.Height = innerSize;
        ProfileImageEllipse.Width = innerSize;
        ProfileImageEllipse.Height = innerSize;

        // Update font sizes
        NameText.FontSize = config.NameFontSize;
        RoleText.FontSize = config.RoleFontSize;
        SecondaryText.FontSize = config.SecondaryFontSize;

        // Update visibility
        EmailText.IsVisible = config.ShowEmail && !string.IsNullOrWhiteSpace(((IPerson?)DataContext)?.Email);
        EnrollmentText.IsVisible = config.ShowEnrollmentDate;
        SecondaryText.IsVisible = config.ShowSecondaryInfo;
        
        // Update image interactivity
        UpdateImageInteractivity();
    }
    
    private void UpdateImageInteractivity()
    {
        if (ProfileImageBorder == null) return;
        
        // Only allow image interaction for actual students, not add cards
        if (DataContext is AddStudentCard)
        {
            ProfileImageBorder.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow);
        }
        else if (DataContext is Student)
        {
            ProfileImageBorder.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        }
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
        // Only respond to primary button releases for actual students (not add cards)
        try
        {
            if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left)
            {
                if (DataContext is Student student)
                {
                    ImageClicked?.Invoke(this, student);
                    e.Handled = true;
                }
                // For add cards (AddStudentCard), we don't do anything - the parent button handles the click
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ProfileImage pointer handler error: {ex.Message}");
        }
    }
}