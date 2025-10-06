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
using SchoolOrganizer.Views.Windows;
using SchoolOrganizer.Services;
using SchoolOrganizer.Models;


namespace SchoolOrganizer.Views.StudentGallery;

public partial class StudentGalleryView : UserControl
{
    public StudentGalleryViewModel? ViewModel => DataContext as StudentGalleryViewModel;

    private const double MIN_CARD_WIDTH = 200;
    private const double MAX_CARD_WIDTH = 280;
    private const double CARD_PADDING = 12; // Margin around each card
    private const double CONTAINER_PADDING = 40; // ScrollViewer padding (20 * 2)
    
    // Current sizing values for newly created containers (20% reduction applied)
    private double _currentCardWidth = 240;
    private double _currentImageSize = 134; // 168 * 0.8
    private double _currentImageRadius = 67; // 84 * 0.8
    private double _currentNameFontSize = 13; // 16 * 0.8
    private double _currentClassFontSize = 10; // 12 * 0.8
    private double _currentMentorFontSize = 8; // 10 * 0.8
    private double _currentPlaceholderFontSize = 54; // 67 * 0.8
    private double _currentCardPadding = 14; // 18 * 0.8
    
    private GlobalKeyboardHandler? _keyboardHandler;
    
    public StudentGalleryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        
        // Subscribe to container prepared events for newly created cards
        var studentsContainer = this.FindControl<ItemsControl>("StudentsContainer");
        if (studentsContainer != null)
        {
            studentsContainer.ContainerPrepared += OnContainerPrepared;
        }
    }

    private DispatcherTimer? _scrollAnimationTimer;
    private void CancelScrollAnimation()
    {
        try
        {
            if (_scrollAnimationTimer != null)
            {
                _scrollAnimationTimer.Stop();
                _scrollAnimationTimer = null;
            }
        }
        catch { }
    }

    private void SmoothScrollToVerticalOffset(ScrollViewer scrollViewer, double targetOffset, int durationMs = 300)
    {
        try
        {
            CancelScrollAnimation();

            var startOffset = scrollViewer.Offset.Y;
            var delta = targetOffset - startOffset;
            if (Math.Abs(delta) < 0.5 || durationMs <= 0)
            {
                scrollViewer.Offset = new Vector(scrollViewer.Offset.X, targetOffset);
                return;
            }

            var startTime = DateTime.UtcNow;
            _scrollAnimationTimer = new DispatcherTimer();
            _scrollAnimationTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps
            _scrollAnimationTimer.Tick += (s, e) =>
            {
                try
                {
                    var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                    var t = Math.Min(1.0, elapsed / durationMs);

                    // Cubic ease-out: 1 - (1 - t)^3
                    var eased = 1.0 - Math.Pow(1.0 - t, 3);

                    var newOffset = startOffset + (delta * eased);
                    scrollViewer.Offset = new Vector(scrollViewer.Offset.X, newOffset);

                    if (t >= 1.0)
                    {
                        CancelScrollAnimation();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Scroll animation error: {ex.Message}");
                    CancelScrollAnimation();
                }
            };

            _scrollAnimationTimer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Smooth scroll start error: {ex.Message}");
        }
    }

    private void SubscribeToViewModelSelections(StudentGalleryViewModel? vm)
    {
        if (vm == null) return;
        vm.PropertyChanged -= ViewModel_PropertyChanged;
        vm.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void UnsubscribeFromViewModelSelections(StudentGalleryViewModel? vm)
    {
        if (vm == null) return;
        vm.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "SelectedStudent")
        {
            // Defer scrolling until layout pass completes
            Dispatcher.UIThread.Post(() => ScrollSelectedStudentIntoCenter(), DispatcherPriority.Background);
        }
        else if (e.PropertyName == "Students")
        {
            // When Students collection changes, re-wire events after UI updates
            // Use a longer delay to ensure the UI has fully rendered the new items
            Dispatcher.UIThread.Post(() => WireUpAllProfileCardEvents(), DispatcherPriority.Loaded);
        }
        else if (e.PropertyName == "DisplayConfig")
        {
            // When DisplayConfig changes, update card layout with new dimensions
            // DisplayConfig changed, updating card layout
            Dispatcher.UIThread.Post(() => UpdateCardLayout(), DispatcherPriority.Render);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // DataContext changed
        
        // Unsubscribe from previous ViewModel if any
        if (sender is StudentGalleryView view && view.Tag is StudentGalleryViewModel oldViewModel)
        {
            // Unsubscribing from old ViewModel
            oldViewModel.AddStudentRequested -= HandleAddStudentRequested;
            oldViewModel.StudentImageChangeRequested -= HandleStudentImageChangeRequested;
            oldViewModel.EditStudentRequested -= HandleEditStudentRequested;
            UnsubscribeFromViewModelSelections(oldViewModel);
            
            // Clean up keyboard handler
            _keyboardHandler?.Dispose();
            _keyboardHandler = null;
        }
        
        // Subscribe to new ViewModel
        if (DataContext is StudentGalleryViewModel viewModel)
        {
            // Subscribing to ViewModel events
            viewModel.AddStudentRequested += HandleAddStudentRequested;
            viewModel.StudentImageChangeRequested += HandleStudentImageChangeRequested;
            viewModel.EditStudentRequested += HandleEditStudentRequested;
            
            // Store reference for cleanup
            Tag = viewModel;
            SubscribeToViewModelSelections(viewModel);
            
            // Initialize keyboard handler
            var searchTextBox = this.FindControl<TextBox>("SearchTextBox");
            if (searchTextBox != null)
            {
                _keyboardHandler?.Dispose(); // Clean up previous handler
                _keyboardHandler = new GlobalKeyboardHandler(viewModel, searchTextBox, this);
                // GlobalKeyboardHandler initialized
            }
            else
            {
                // SearchTextBox not found, keyboard handler not initialized
            }
            
            // Ensure ItemsControl container events are properly subscribed
            var studentsContainer = this.FindControl<ItemsControl>("StudentsContainer");
            if (studentsContainer != null)
            {
                studentsContainer.ContainerPrepared -= OnContainerPrepared;
                studentsContainer.ContainerPrepared += OnContainerPrepared;
            }
        }
        else
        {
            // DataContext is not StudentGalleryViewModel
        }
    }

    /// <summary>
    /// Re-initializes the keyboard handler and ProfileCard events to ensure everything works after returning from detailed view
    /// </summary>
    public void ReinitializeKeyboardHandler()
    {
        // Reinitializing keyboard handler
        if (DataContext is StudentGalleryViewModel viewModel)
        {
            var searchTextBox = this.FindControl<TextBox>("SearchTextBox");
            if (searchTextBox != null)
            {
                _keyboardHandler?.Dispose(); // Clean up previous handler
                _keyboardHandler = new GlobalKeyboardHandler(viewModel, searchTextBox, this);
                // GlobalKeyboardHandler re-initialized
            }
            else
            {
                // SearchTextBox not found, keyboard handler not re-initialized
            }
            
            // Also re-wire ProfileCard events to ensure double-click works
            WireUpAllProfileCardEvents();
            // ProfileCard events re-wired
        }
        else
        {
            // DataContext is not StudentGalleryViewModel, cannot re-initialize keyboard handler
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
            // Add student requested
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
                    result.Mentors,
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
                    result.Mentors,
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
        // StudentGalleryView loaded
        
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

        // Subscribe to ViewModel selection changes
        SubscribeToViewModelSelections(ViewModel);
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
        // Recenter selected student on size changes
        Dispatcher.UIThread.Post(() => ScrollSelectedStudentIntoCenter(), DispatcherPriority.Background);
        
        // Update display level based on new window size
        if (ViewModel != null)
        {
            ViewModel.OnWindowResized();
        }
    }

    private void UpdateCardLayout()
    {
        try
        {
            var availableWidth = Bounds.Width;
            if (availableWidth <= 0) return;

            // Get the DisplayConfig card width instead of calculating it
            var viewModel = DataContext as StudentGalleryViewModel;
            var displayConfig = viewModel?.DisplayConfig ?? ProfileCardDisplayConfig.GetConfig(ProfileCardDisplayLevel.Medium);
            var cardWidth = displayConfig.CardWidth;
            
            // Updating card layout
            
            // Apply the DisplayConfig sizing to all cards
            ApplyCardSizing(cardWidth);

            // Layout updated successfully
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

        // Use DisplayConfig dimensions instead of calculated ones to maintain proper proportions
        var viewModel = DataContext as StudentGalleryViewModel;
        var displayConfig = viewModel?.DisplayConfig ?? ProfileCardDisplayConfig.GetConfig(ProfileCardDisplayLevel.Medium);
        
        var imageSize = displayConfig.ImageSize;
        var imageRadius = imageSize / 2;
        var nameFontSize = displayConfig.NameFontSize;
        var classFontSize = displayConfig.RoleFontSize;
        var mentorFontSize = displayConfig.SecondaryFontSize;
        var placeholderFontSize = imageSize * 0.4;
        var cardPadding = 15; // Use standard padding

        // Applying card sizing
        // Updating card containers

        // Update all existing card containers
        int updatedContainers = 0;
        for (int i = 0; i < studentsContainer.ItemCount; i++)
        {
            var container = studentsContainer.ContainerFromIndex(i);
            if (container != null)
            {
                UpdateCardElements(container, cardWidth, imageSize, imageRadius, nameFontSize, 
                                 classFontSize, mentorFontSize, placeholderFontSize, cardPadding);
                updatedContainers++;
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

        // Card sizing completed
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
            if (Math.Abs(cardPadding - 15) > 1)
            {
                var cardButton = FindNamedChild<Button>(container, "CardButton");
                if (cardButton != null)
                {
                    cardButton.Padding = new Thickness(cardPadding, cardPadding * 0.75);
                }
            }

            // Update image elements only if size changed significantly
            if (Math.Abs(imageSize - 90) > 1)
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

            if (Math.Abs(placeholderFontSize - 36) > 1)
            {
                var placeholderText = FindNamedChild<TextBlock>(container, "PlaceholderText");
                if (placeholderText != null)
                {
                    placeholderText.FontSize = placeholderFontSize;
                }
            }

            // Update info container max width if needed
            if (Math.Abs(textMaxWidth - 210) > 1)
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
            // Note: With the new card system, event wiring is handled directly in AXAML
            // This method is kept for backwards compatibility but does minimal work
            System.Diagnostics.Debug.WriteLine("WireUpProfileCardEvents called (new card system uses AXAML event handlers)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error wiring up ProfileCard events: {ex.Message}");
        }
    }

    private async void OnProfileCardImageClicked(object? sender, SchoolOrganizer.Models.Student student)
    {
        // ProfileCard ImageClicked event fired
        if (student != null)
        {
            await HandleStudentImageChange(student);
        }
    }

    private async void OnProfileCardDoubleClicked(object? sender, SchoolOrganizer.Models.Student student)
    {
        // ProfileCard CardDoubleClicked event fired
        if (student != null && ViewModel != null)
        {
            // Clear search first, then set it to the student's name to ensure the search triggers
            ViewModel.SearchText = string.Empty;
            await Task.Delay(10); // Small delay to ensure the clear takes effect
            ViewModel.SearchText = student.Name;
            // Set search text to show big view mode
        }
    }

    private async void OnDetailedProfileImageClicked(object? sender, SchoolOrganizer.Models.Student student)
    {
        await HandleStudentImageChange(student);
    }

    private async void OnProfileImageClicked(object? sender, SchoolOrganizer.Models.Student student)
    {
        await HandleStudentImageChange(student);
    }

    private async void OnProfileImageUpdated(object? sender, (SchoolOrganizer.Models.Student student, string imagePath) args)
    {
        // ProfileCard ProfileImageUpdated event fired
        if (args.student != null && !string.IsNullOrEmpty(args.imagePath) && ViewModel != null)
        {
            System.Diagnostics.Debug.WriteLine($"OnProfileImageUpdated: Updating student {args.student.Id} with new image: {args.imagePath}");
            await ViewModel.UpdateStudentImage(args.student, args.imagePath);
        }
    }

    private void OnBackToGalleryRequested(object? sender, EventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.BackToGalleryCommand.Execute(null);
            // Re-initialize keyboard handler to ensure keyboard detection works after returning from detailed view
            ReinitializeKeyboardHandler();
        }
    }

    private void WireUpAllProfileCardEvents()
    {
        try
        {
            // Note: With the new card system, event wiring is handled directly in AXAML
            // This method is kept for backwards compatibility but does minimal work
            System.Diagnostics.Debug.WriteLine("WireUpAllProfileCardEvents called (new card system uses AXAML event handlers)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error wiring up all ProfileCard events: {ex.Message}");
        }
    }

        private void ScrollSelectedStudentIntoCenter()
        {
            try
            {
                if (ViewModel == null || ViewModel.SelectedStudent == null) return;

                var scrollViewer = this.FindControl<ScrollViewer>("StudentsScrollViewer");
                var itemsControl = this.FindControl<ItemsControl>("StudentsContainer");
                if (scrollViewer == null || itemsControl == null) return;

                // Find the container for the selected student
                int index = itemsControl.Items.Cast<object>().ToList().FindIndex(i =>
                {
                    if (i is SchoolOrganizer.Models.Student s)
                        return s.Id == ViewModel.SelectedStudent.Id;
                    return false;
                });

                if (index < 0) return;

                var container = itemsControl.ContainerFromIndex(index) as Control;
                if (container == null) return;

                // Transform container bounds to ScrollViewer coordinate space
                var containerBounds = container.Bounds;
                var containerPoint = container.TranslatePoint(new Point(0, 0), scrollViewer);
                if (containerPoint == null) return;

                double containerCenterY = containerPoint.Value.Y + (containerBounds.Height / 2.0);

                // Find header bottom and details top in window coordinates
                var header = this.FindControl<Border>("HeaderBorder");
                var detailsHost = this.FindControl<Border>("SelectedDetailsHost");

                // Get TopLevel (window) for coordinate transforms
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var headerBottomInWindow = 0.0;
                if (header != null)
                {
                    var headerPoint = header.TranslatePoint(new Point(0, header.Bounds.Height), topLevel);
                    if (headerPoint != null)
                        headerBottomInWindow = headerPoint.Value.Y;
                }

                double detailsTopInWindow = double.NaN;
                if (detailsHost != null)
                {
                    var detailsPoint = detailsHost.TranslatePoint(new Point(0, 0), topLevel);
                    if (detailsPoint != null)
                        detailsTopInWindow = detailsPoint.Value.Y;
                }

                // If details not visible use bottom of window as fallback
                double windowHeight = topLevel.Bounds.Height;

                double areaTop = headerBottomInWindow;
                double areaBottom = !double.IsNaN(detailsTopInWindow) ? detailsTopInWindow : windowHeight;

                // Midpoint in window coords
                double midpointWindowY = (areaTop + areaBottom) / 2.0;

                // Translate midpoint into ScrollViewer's content coordinate space by comparing to scroll viewport top in window coords
                var scrollViewportTopInWindow = scrollViewer.TranslatePoint(new Point(0, 0), topLevel)?.Y ?? 0;
                double midpointRelativeToScroll = midpointWindowY - scrollViewportTopInWindow;

                // Compute required vertical offset so containerCenterY aligns with midpointRelativeToScroll
                double currentOffset = scrollViewer.Offset.Y;
                double targetOffset = currentOffset + (containerCenterY - midpointRelativeToScroll);

                // Clamp target offset
                targetOffset = Math.Max(0, Math.Min(targetOffset, scrollViewer.Extent.Height - scrollViewer.Viewport.Height));

                // Smooth scroll to target offset
                SmoothScrollToVerticalOffset(scrollViewer, targetOffset, 320);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scrolling selected student into center: {ex.Message}");
            }
        }
}
