using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchoolOrganizer.ViewModels;
using System.Threading.Tasks;
using Avalonia.Layout;
using Avalonia.Controls.Primitives;
using System.Linq;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls.Shapes;


namespace SchoolOrganizer.Views;

public partial class StudentGalleryView : UserControl
{
    public StudentGalleryViewModel? ViewModel => DataContext as StudentGalleryViewModel;

    private const double MIN_CARD_WIDTH = 200;
    private const double MAX_CARD_WIDTH = 280;
    private const double CARD_PADDING = 12; // Margin around each card
    private const double CONTAINER_PADDING = 40; // ScrollViewer padding (20 * 2)
    
    // Current sizing values for newly created containers
    private double _currentCardWidth = 240;
    private double _currentImageSize = 168;
    private double _currentImageRadius = 84;
    private double _currentNameFontSize = 16;
    private double _currentClassFontSize = 12;
    private double _currentMentorFontSize = 10;
    private double _currentPlaceholderFontSize = 67;
    private double _currentCardPadding = 18;
    
    public StudentGalleryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        
        // Subscribe to container prepared events for newly created cards
        var profileCards = this.FindControl<ItemsControl>("ProfileCards");
        if (profileCards != null)
        {
            profileCards.ContainerPrepared += OnContainerPrepared;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("StudentGalleryView DataContextChanged called");
        
        // Unsubscribe from previous ViewModel if any
        if (sender is StudentGalleryView view && view.Tag is StudentGalleryViewModel oldViewModel)
        {
            System.Diagnostics.Debug.WriteLine("Unsubscribing from old ViewModel");
            oldViewModel.AddStudentRequested -= HandleAddStudentRequested;
            oldViewModel.StudentImageChangeRequested -= HandleStudentImageChangeRequested;
            oldViewModel.EditStudentRequested -= HandleEditStudentRequested;
        }
        
        // Subscribe to new ViewModel
        if (DataContext is StudentGalleryViewModel viewModel)
        {
            System.Diagnostics.Debug.WriteLine("ViewModel found via DataContextChanged, subscribing to events");
            viewModel.AddStudentRequested += HandleAddStudentRequested;
            viewModel.StudentImageChangeRequested += HandleStudentImageChangeRequested;
            viewModel.EditStudentRequested += HandleEditStudentRequested;
            
            // Store reference for cleanup
            Tag = viewModel;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"DataContext is not StudentGalleryViewModel, it's: {DataContext?.GetType().Name ?? "null"}");
        }
    }

    private async void HandleAddStudentRequested(object? sender, EventArgs e)
    {
        await HandleAddStudent();
    }

    private async System.Threading.Tasks.Task HandleAddStudent()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Add student requested");
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow == null || ViewModel == null) return;

            var addWindow = new AddStudentWindow();
            addWindow.LoadOptionsFromStudents(ViewModel.AllStudents);
            var result = await addWindow.ShowDialog<AddStudentWindow.AddedStudentResult?>(parentWindow);
            if (result != null)
            {
                await ViewModel.AddNewStudentAsync(
                    result.Name,
                    result.ClassName,
                    result.Mentor,
                    result.Email,
                    result.EnrollmentDate,
                    result.PicturePath
                );
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding student: {ex.Message}");
        }
    }

    private async void HandleStudentImageChangeRequested(object? sender, SchoolOrganizer.Models.Student student)
    {
        await HandleStudentImageChange(student);
    }

    private async Task HandleStudentImageChange(SchoolOrganizer.Models.Student student)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"HandleStudentImageChange called for student {student.Id} ({student.Name})");
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow == null) return;
            
            var newPath = await ImageCropWindow.ShowForStudentAsync(parentWindow, student.Id);
            System.Diagnostics.Debug.WriteLine($"ImageCropWindow returned path: {newPath}");
            
            if (!string.IsNullOrEmpty(newPath) && DataContext is StudentGalleryViewModel vm)
            {
                System.Diagnostics.Debug.WriteLine("Calling UpdateStudentImage on ViewModel");
                await vm.UpdateStudentImage(student, newPath);
                System.Diagnostics.Debug.WriteLine("UpdateStudentImage completed");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error changing image: {ex.Message}");
        }
    }

    private async void HandleEditStudentRequested(object? sender, SchoolOrganizer.Models.Student student)
    {
        await HandleEditStudent(student);
    }

    private async Task HandleEditStudent(SchoolOrganizer.Models.Student student)
    {
        try
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow == null || ViewModel == null) return;

            var editWindow = new AddStudentWindow();
            editWindow.LoadOptionsFromStudents(ViewModel.AllStudents);
            editWindow.InitializeForEdit(student);
            var result = await editWindow.ShowDialog<AddStudentWindow.AddedStudentResult?>(parentWindow);
            if (result != null)
            {
                await ViewModel.UpdateExistingStudentAsync(
                    student,
                    result.Name,
                    result.ClassName,
                    result.Mentor,
                    result.Email,
                    result.EnrollmentDate,
                    result.PicturePath
                );
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error editing student: {ex.Message}");
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Ensure we have the container prepared event subscribed after loading
        var profileCards = this.FindControl<ItemsControl>("ProfileCards");
        if (profileCards != null)
        {
            profileCards.ContainerPrepared -= OnContainerPrepared; // Avoid double subscription
            profileCards.ContainerPrepared += OnContainerPrepared;
        }
        
        UpdateCardLayout();
    }

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        UpdateCardElements(e.Container, _currentCardWidth, _currentImageSize, _currentImageRadius,
                         _currentNameFontSize, _currentClassFontSize, _currentMentorFontSize,
                         _currentPlaceholderFontSize, _currentCardPadding);
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateCardLayout();
    }

    private void UpdateCardLayout()
    {
        try
        {
            var availableWidth = Bounds.Width;
            if (availableWidth <= 0) return;

            // Account for scrollviewer padding and potential scrollbar
            var usableWidth = availableWidth - CONTAINER_PADDING - 20; // 20px for potential scrollbar

            // Calculate optimal card width and columns
            var (cardWidth, columns) = CalculateOptimalLayout(usableWidth);
            
            // Apply the calculated sizing to all cards
            ApplyCardSizing(cardWidth);

            System.Diagnostics.Debug.WriteLine($"Layout updated: Width={availableWidth:F0}, Usable={usableWidth:F0}, CardWidth={cardWidth:F0}, Columns={columns}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating card layout: {ex.Message}");
        }
    }

    private (double cardWidth, int columns) CalculateOptimalLayout(double usableWidth)
    {
        // Start with maximum columns possible at minimum card width
        var maxColumns = (int)Math.Floor(usableWidth / (MIN_CARD_WIDTH + (CARD_PADDING * 2)));
        maxColumns = Math.Max(1, maxColumns); // At least 1 column

        // Try different column counts to find the best fit
        for (int cols = maxColumns; cols >= 1; cols--)
        {
            var totalPadding = cols * (CARD_PADDING * 2);
            var availableForCards = usableWidth - totalPadding;
            var cardWidth = availableForCards / cols;

            // If card width is within our acceptable range, use it
            if (cardWidth >= MIN_CARD_WIDTH && cardWidth <= MAX_CARD_WIDTH)
            {
                return (cardWidth, cols);
            }
            
            // If card would be too wide, cap it at MAX_CARD_WIDTH
            if (cardWidth > MAX_CARD_WIDTH)
            {
                return (MAX_CARD_WIDTH, cols);
            }
        }

        // Fallback: use minimum card width with 1 column
        return (MIN_CARD_WIDTH, 1);
    }

    private void ApplyCardSizing(double cardWidth)
    {
        var profileCards = this.FindControl<ItemsControl>("ProfileCards");
        if (profileCards == null) return;

        // Calculate proportional sizes based on card width
        var imageSize = Math.Max(120, cardWidth * 0.7); // Image is 70% of card width, min 120px
        var imageRadius = imageSize / 2;
        var nameFontSize = Math.Max(14, cardWidth * 0.065); // Proportional to card width
        var classFontSize = Math.Max(11, cardWidth * 0.05);
        var mentorFontSize = Math.Max(10, cardWidth * 0.043);
        var placeholderFontSize = Math.Max(48, imageSize * 0.4);
        var cardPadding = Math.Max(15, cardWidth * 0.075);

        // Update all existing card containers
        for (int i = 0; i < profileCards.ItemCount; i++)
        {
            var container = profileCards.ContainerFromIndex(i);
            if (container != null)
            {
                UpdateCardElements(container, cardWidth, imageSize, imageRadius, nameFontSize, 
                                 classFontSize, mentorFontSize, placeholderFontSize, cardPadding);
            }
        }

        // Store values for newly created containers
        _currentCardWidth = cardWidth;
        _currentImageSize = imageSize;
        _currentImageRadius = imageRadius;
        _currentNameFontSize = nameFontSize;
        _currentClassFontSize = classFontSize;
        _currentMentorFontSize = mentorFontSize;
        _currentPlaceholderFontSize = placeholderFontSize;
        _currentCardPadding = cardPadding;
    }

    private void UpdateCardElements(Control container, double cardWidth, double imageSize, double imageRadius,
                                  double nameFontSize, double classFontSize, double mentorFontSize, 
                                  double placeholderFontSize, double cardPadding)
    {
        try
        {
            // Find and update the card border
            var cardBorder = FindNamedChild<Border>(container, "StudentCard");
            if (cardBorder != null)
            {
                cardBorder.Width = cardWidth;
                // Let height be determined by content to avoid empty space at the bottom
                cardBorder.Height = double.NaN;
            }

            // Find and update the button padding
            var cardButton = FindNamedChild<Button>(container, "CardButton");
            if (cardButton != null)
            {
                cardButton.Padding = new Thickness(cardPadding, cardPadding * 0.75);
            }

            // Find and update image container
            var imageContainer = FindNamedChild<Grid>(container, "ImageContainer");
            if (imageContainer != null)
            {
                imageContainer.Width = imageSize;
                imageContainer.Height = imageSize;
            }

            // Find and update image border
            var imageBorder = FindNamedChild<Border>(container, "ImageBorder");
            if (imageBorder != null)
            {
                imageBorder.Width = imageSize;
                imageBorder.Height = imageSize;
                imageBorder.CornerRadius = new CornerRadius(imageRadius);
            }

            // Find and update image button
            var imageButton = FindNamedChild<Button>(container, "ImageButton");
            if (imageButton != null)
            {
                imageButton.Width = imageSize;
                imageButton.Height = imageSize;
                imageButton.CornerRadius = new CornerRadius(imageRadius);
            }

            // Ensure the ellipse (image mask) is constrained to the placeholder size
            var profileEllipse = FindNamedChild<Ellipse>(container, "ProfileEllipse");
            if (profileEllipse != null)
            {
                profileEllipse.Width = imageSize;
                profileEllipse.Height = imageSize;
            }

            // Find and update text elements
            var nameText = FindNamedChild<TextBlock>(container, "NameText");
            if (nameText != null)
            {
                nameText.FontSize = nameFontSize;
                nameText.MaxWidth = cardWidth - (cardPadding * 2);
            }

            var classText = FindNamedChild<TextBlock>(container, "ClassText");
            if (classText != null)
            {
                classText.FontSize = classFontSize;
                classText.MaxWidth = cardWidth - (cardPadding * 2);
            }

            var mentorText = FindNamedChild<TextBlock>(container, "MentorText");
            if (mentorText != null)
            {
                mentorText.FontSize = mentorFontSize;
                mentorText.MaxWidth = cardWidth - (cardPadding * 2);
            }

            var placeholderText = FindNamedChild<TextBlock>(container, "PlaceholderText");
            if (placeholderText != null)
            {
                placeholderText.FontSize = placeholderFontSize;
            }

            // Find and update info container
            var infoContainer = FindNamedChild<StackPanel>(container, "InfoContainer");
            if (infoContainer != null)
            {
                infoContainer.MaxWidth = cardWidth - (cardPadding * 2);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating card elements: {ex.Message}");
        }
    }

    private T? FindNamedChild<T>(Control parent, string name) where T : Control
    {
        try
        {
            return parent.GetVisualDescendants().OfType<T>().FirstOrDefault(x => x.Name == name);
        }
        catch
        {
            return null;
        }
    }
}
