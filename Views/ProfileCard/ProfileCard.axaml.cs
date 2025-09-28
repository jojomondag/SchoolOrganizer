using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchoolOrganizer.Models;
using SchoolOrganizer.ViewModels;
using SchoolOrganizer.Views.StudentGallery;

namespace SchoolOrganizer.Views.ProfileCard;

public partial class ProfileCard : UserControl
{
    public event EventHandler<Student>? ImageClicked;
    public event EventHandler<Student>? CardDoubleClicked;

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

    private async void OnPersonCardDoubleTapped(object? sender, RoutedEventArgs e)
    {
        // Only respond to double-clicks for actual students (not add cards)
        try
        {
            System.Diagnostics.Debug.WriteLine("ProfileCard double-tapped - OnPersonCardDoubleTapped called");
            if (DataContext is Student student)
            {
                System.Diagnostics.Debug.WriteLine($"ProfileCard double-clicked for student: {student.Name}");
                
                // Fire the event for external handlers
                CardDoubleClicked?.Invoke(this, student);
                
                // Find the parent StudentGalleryViewModel through the visual tree
                var parentViewModel = FindParentStudentGalleryViewModel();
                if (parentViewModel != null)
                {
                    System.Diagnostics.Debug.WriteLine("ProfileCard found parent ViewModel, handling double-click directly");
                    // Clear search first, then set it to the student's name to ensure the search triggers
                    parentViewModel.SearchText = string.Empty;
                    await Task.Delay(10); // Small delay to ensure the clear takes effect
                    parentViewModel.SearchText = student.Name;
                    System.Diagnostics.Debug.WriteLine($"ProfileCard set search text to '{student.Name}' to show big view mode");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ProfileCard could not find parent ViewModel - cannot handle double-click directly");
                }
                
                e.Handled = true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"ProfileCard double-clicked but DataContext is not a Student, it's: {DataContext?.GetType().Name ?? "null"}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ProfileCard double-tap handler error: {ex.Message}");
        }
    }

    private StudentGalleryViewModel? FindParentStudentGalleryViewModel()
    {
        try
        {
            // Walk up the visual tree to find the StudentGalleryView
            var current = this.Parent;
            while (current != null)
            {
                if (current is StudentGalleryView galleryView)
                {
                    return galleryView.ViewModel;
                }
                current = current.Parent;
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error finding parent ViewModel: {ex.Message}");
            return null;
        }
    }
}