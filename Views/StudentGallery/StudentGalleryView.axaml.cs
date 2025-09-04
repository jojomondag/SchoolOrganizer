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
using Avalonia.Threading;
using Avalonia.Input;
using SchoolOrganizer.Views.ProfileCard;


namespace SchoolOrganizer.Views.StudentGallery;

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
        
        // Enable keyboard input handling
        Focusable = true;
        KeyDown += OnKeyDown;
        
        // Subscribe to container prepared events for newly created cards
        var studentsContainer = this.FindControl<ItemsControl>("StudentsContainer");
        if (studentsContainer != null)
        {
            studentsContainer.ContainerPrepared += OnContainerPrepared;
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
        var studentsContainer = this.FindControl<ItemsControl>("StudentsContainer");
        if (studentsContainer != null)
        {
            studentsContainer.ContainerPrepared -= OnContainerPrepared; // Avoid double subscription
            studentsContainer.ContainerPrepared += OnContainerPrepared;
        }
        
        UpdateCardLayout();
        
        // Wire up ProfileCard events for any existing cards
        WireUpAllProfileCardEvents();
        
        // Set focus to this control so it can receive keyboard events
        Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Check if the key pressed is alphanumeric or common typing keys
        if (IsTypingKey(e.Key))
        {
            // Find the search TextBox by name
            var searchTextBox = this.FindControl<TextBox>("SearchTextBox");
            
            if (searchTextBox != null && !searchTextBox.IsFocused)
            {
                // Focus the search box
                searchTextBox.Focus();
                
                // Get the character representation of the key
                var keyChar = GetCharFromKey(e.Key, e.KeyModifiers);
                if (keyChar != null)
                {
                    // Set the search text to start with the typed character
                    if (ViewModel != null)
                    {
                        ViewModel.SearchText = keyChar;
                        // Position cursor at the end
                        Dispatcher.UIThread.Post(() =>
                        {
                            searchTextBox.CaretIndex = searchTextBox.Text?.Length ?? 0;
                        }, DispatcherPriority.Background);
                    }
                }
                
                e.Handled = true;
            }
        }
    }

    private static bool IsTypingKey(Key key)
    {
        // Check for alphanumeric keys and common typing keys
        return (key >= Key.A && key <= Key.Z) ||
               (key >= Key.D0 && key <= Key.D9) ||
               (key >= Key.NumPad0 && key <= Key.NumPad9) ||
               key == Key.Space ||
               key == Key.OemMinus ||
               key == Key.OemPlus ||
               key == Key.OemPeriod ||
               key == Key.OemComma;
    }

    private static string? GetCharFromKey(Key key, KeyModifiers modifiers)
    {
        // Handle letters
        if (key >= Key.A && key <= Key.Z)
        {
            var letter = (char)('a' + (key - Key.A));
            return (modifiers & KeyModifiers.Shift) != 0 ? letter.ToString().ToUpper() : letter.ToString();
        }
        
        // Handle numbers
        if (key >= Key.D0 && key <= Key.D9)
        {
            if ((modifiers & KeyModifiers.Shift) != 0)
            {
                // Handle shift+number symbols
                return key switch
                {
                    Key.D1 => "!",
                    Key.D2 => "@",
                    Key.D3 => "#",
                    Key.D4 => "$",
                    Key.D5 => "%",
                    Key.D6 => "^",
                    Key.D7 => "&",
                    Key.D8 => "*",
                    Key.D9 => "(",
                    Key.D0 => ")",
                    _ => null
                };
            }
            else
            {
                return ((char)('0' + (key - Key.D0))).ToString();
            }
        }
        
        // Handle numpad numbers
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return ((char)('0' + (key - Key.NumPad0))).ToString();
        }
        
        // Handle other keys
        return key switch
        {
            Key.Space => " ",
            Key.OemMinus => (modifiers & KeyModifiers.Shift) != 0 ? "_" : "-",
            Key.OemPlus => (modifiers & KeyModifiers.Shift) != 0 ? "+" : "=",
            Key.OemPeriod => (modifiers & KeyModifiers.Shift) != 0 ? ">" : ".",
            Key.OemComma => (modifiers & KeyModifiers.Shift) != 0 ? "<" : ",",
            _ => null
        };
    }

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        // Wire up ProfileCard ImageClicked event
        WireUpProfileCardEvents(e.Container);
        
        // Only apply dynamic styling if current dimensions differ significantly from defaults
        var needsUpdate = Math.Abs(_currentCardWidth - 240) > 5 || 
                         Math.Abs(_currentImageSize - 168) > 5;
        
        if (needsUpdate)
        {
            // Apply styling immediately to prevent flickering
            UpdateCardElements(e.Container, _currentCardWidth, _currentImageSize, _currentImageRadius,
                             _currentNameFontSize, _currentClassFontSize, _currentMentorFontSize,
                             _currentPlaceholderFontSize, _currentCardPadding);
        }
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
        var studentsContainer = this.FindControl<ItemsControl>("StudentsContainer");
        if (studentsContainer == null) return;

        // Calculate proportional sizes based on card width
        var imageSize = Math.Max(120, cardWidth * 0.7); // Image is 70% of card width, min 120px
        var imageRadius = imageSize / 2;
        var nameFontSize = Math.Max(14, cardWidth * 0.065); // Proportional to card width
        var classFontSize = Math.Max(11, cardWidth * 0.05);
        var mentorFontSize = Math.Max(10, cardWidth * 0.043);
        var placeholderFontSize = Math.Max(48, imageSize * 0.4);
        var cardPadding = Math.Max(15, cardWidth * 0.075);

        // Update all existing card containers
        for (int i = 0; i < studentsContainer.ItemCount; i++)
        {
            var container = studentsContainer.ContainerFromIndex(i);
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
            // Only update elements that need to change from their default values
            var textMaxWidth = cardWidth - (cardPadding * 2);
            
            // Update card border width only if different from default
            if (Math.Abs(cardWidth - 240) > 1)
            {
                var cardBorder = FindNamedChild<Border>(container, "StudentCard");
                if (cardBorder != null)
                {
                    cardBorder.Width = cardWidth;
                }
            }

            // Update button padding only if different from default
            if (Math.Abs(cardPadding - 18) > 1)
            {
                var cardButton = FindNamedChild<Button>(container, "CardButton");
                if (cardButton != null)
                {
                    cardButton.Padding = new Thickness(cardPadding, cardPadding * 0.75);
                }
            }

            // Update image elements only if size changed significantly
            if (Math.Abs(imageSize - 168) > 1)
            {
                var imageContainer = FindNamedChild<Grid>(container, "ImageContainer");
                if (imageContainer != null)
                {
                    imageContainer.Width = imageSize;
                    imageContainer.Height = imageSize;
                }

                var imageBorder = FindNamedChild<Border>(container, "ImageBorder");
                if (imageBorder != null)
                {
                    imageBorder.Width = imageSize;
                    imageBorder.Height = imageSize;
                    imageBorder.CornerRadius = new CornerRadius(imageRadius);
                }

                var imageButton = FindNamedChild<Button>(container, "ImageButton");
                if (imageButton != null)
                {
                    imageButton.Width = imageSize;
                    imageButton.Height = imageSize;
                    imageButton.CornerRadius = new CornerRadius(imageRadius);
                }

                var profileEllipse = FindNamedChild<Ellipse>(container, "ProfileEllipse");
                if (profileEllipse != null)
                {
                    profileEllipse.Width = imageSize;
                    profileEllipse.Height = imageSize;
                }
            }

            // Update text elements only if font sizes changed
            if (Math.Abs(nameFontSize - 16) > 0.5)
            {
                var nameText = FindNamedChild<TextBlock>(container, "NameText");
                if (nameText != null)
                {
                    nameText.FontSize = nameFontSize;
                    nameText.MaxWidth = textMaxWidth;
                }
            }

            if (Math.Abs(classFontSize - 12) > 0.5)
            {
                var classText = FindNamedChild<TextBlock>(container, "ClassText");
                if (classText != null)
                {
                    classText.FontSize = classFontSize;
                    classText.MaxWidth = textMaxWidth;
                }
            }

            if (Math.Abs(mentorFontSize - 10) > 0.5)
            {
                var mentorText = FindNamedChild<TextBlock>(container, "MentorText");
                if (mentorText != null)
                {
                    mentorText.FontSize = mentorFontSize;
                    mentorText.MaxWidth = textMaxWidth;
                }
            }

            if (Math.Abs(placeholderFontSize - 67) > 1)
            {
                var placeholderText = FindNamedChild<TextBlock>(container, "PlaceholderText");
                if (placeholderText != null)
                {
                    placeholderText.FontSize = placeholderFontSize;
                }
            }

            // Update info container max width if needed
            if (Math.Abs(textMaxWidth - 204) > 1)
            {
                var infoContainer = FindNamedChild<StackPanel>(container, "InfoContainer");
                if (infoContainer != null)
                {
                    infoContainer.MaxWidth = textMaxWidth;
                }
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

    private void WireUpProfileCardEvents(Control container)
    {
        try
        {
            // Find the ProfileCard within the container
            var profileCard = container.FindDescendantOfType<ProfileCard.ProfileCard>();
            if (profileCard != null)
            {
                // Remove existing handler to avoid duplicates
                profileCard.ImageClicked -= OnProfileCardImageClicked;
                // Add the handler
                profileCard.ImageClicked += OnProfileCardImageClicked;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error wiring up ProfileCard events: {ex.Message}");
        }
    }

    private async void OnProfileCardImageClicked(object? sender, SchoolOrganizer.Models.Student student)
    {
        await HandleStudentImageChange(student);
    }

    private void WireUpAllProfileCardEvents()
    {
        try
        {
            var studentsContainer = this.FindControl<ItemsControl>("StudentsContainer");
            if (studentsContainer != null)
            {
                // Find all ProfileCard controls in the ItemsControl
                var profileCards = studentsContainer.GetVisualDescendants().OfType<ProfileCard.ProfileCard>();
                foreach (var profileCard in profileCards)
                {
                    // Remove existing handler to avoid duplicates
                    profileCard.ImageClicked -= OnProfileCardImageClicked;
                    // Add the handler
                    profileCard.ImageClicked += OnProfileCardImageClicked;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error wiring up all ProfileCard events: {ex.Message}");
        }
    }
}
