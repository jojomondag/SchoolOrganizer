using System;
using System.Collections.Generic;
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
using SchoolOrganizer.Views.ProfileCards;
using SchoolOrganizer.Views.Windows;
using SchoolOrganizer.Views.Windows.ImageCrop;
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
            // Students collection changed - layout will automatically update via bindings
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

            // Check if we have GoogleAuthService available for classroom import
            var addWindow = ViewModel.AuthService != null 
                ? new AddStudentWindow(ViewModel.AuthService)
                : new AddStudentWindow();
            
            addWindow.LoadOptionsFromStudents(ViewModel.AllStudents);
            var result = await addWindow.ShowDialog<object?>(parentWindow);
            
            if (result != null)
            {
                // Handle single student result (manual mode)
                if (result is AddStudentWindow.AddedStudentResult singleResult)
                {
                    await ViewModel.AddNewStudentAsync(
                        singleResult.Name,
                        singleResult.ClassName,
                        singleResult.Mentors,
                        singleResult.Email,
                        singleResult.EnrollmentDate,
                        singleResult.PicturePath
                    );
                }
                // Handle multiple students result (classroom import mode)
                else if (result is List<AddStudentWindow.AddedStudentResult> multipleResults)
                {
                    await ViewModel.AddMultipleStudentsAsync(multipleResults);
                }
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

            // Pass existing ORIGINAL image and crop settings to the cropper
            var result = await ImageCropWindow.ShowForStudentAsync(
                parentWindow,
                student.Id,
                student.OriginalImagePath,  // Load the original, not the cropped result
                student.CropSettings);

            System.Diagnostics.Debug.WriteLine($"ImageCropWindow returned path: {result.imagePath}, settings: {(result.cropSettings != null ? "present" : "null")}, original: {result.originalImagePath}");

            if (!string.IsNullOrEmpty(result.imagePath) && DataContext is StudentGalleryViewModel vm)
            {
                System.Diagnostics.Debug.WriteLine("Calling UpdateStudentImage on ViewModel");
                await vm.UpdateStudentImage(student, result.imagePath, result.cropSettings, result.originalImagePath);
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

        // Subscribe to ViewModel selection changes
        SubscribeToViewModelSelections(ViewModel);
    }

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
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

            // Disable dynamic sizing to maintain consistency
            // Let XAML templates handle sizing with their hardcoded dimensions
            // This ensures cards look the same whether loaded initially or after search
            
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
        // Disabled dynamic card sizing to maintain consistency
        // Cards will use their XAML template dimensions
        // This ensures consistent appearance between initial load and search results
    }

    private void UpdateCardElements(Control container, double cardWidth, double imageSize, double imageRadius,
                                  double nameFontSize, double classFontSize, double mentorFontSize, 
                                  double placeholderFontSize, double cardPadding)
    {
        // Disabled dynamic element updates to maintain consistency
        // Cards will use their XAML template styling
        // This ensures consistent appearance between initial load and search results
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

    // Event handler for when profile image is clicked (opens ImageCropWindow)
    private async void OnProfileImageClicked(object? sender, SchoolOrganizer.Models.Student student)
    {
        await HandleStudentImageChange(student);
    }

    // Event handler for detailed view profile image clicks
    private async void OnDetailedProfileImageClicked(object? sender, SchoolOrganizer.Models.Student student)
    {
        await HandleStudentImageChange(student);
    }

    // Event handler for when a profile image has been updated via ImageCropWindow
    private async void OnProfileImageUpdated(object? sender, (SchoolOrganizer.Models.Student student, string imagePath) args)
    {
        if (args.student != null && !string.IsNullOrEmpty(args.imagePath) && ViewModel != null)
        {
            System.Diagnostics.Debug.WriteLine($"OnProfileImageUpdated: Updating student {args.student.Id} with new image: {args.imagePath}, cropSettings: {args.student.CropSettings ?? "NULL"}, original: {args.student.OriginalImagePath ?? "NULL"}");
            await ViewModel.UpdateStudentImage(args.student, args.imagePath, args.student.CropSettings, args.student.OriginalImagePath);
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
