using System;
using System.Collections.Generic;
using System.IO;
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
using SchoolOrganizer.Services.Utilities;
using SchoolOrganizer.Models;
using Serilog;


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
    private double _currentTeacherFontSize = 8; // 10 * 0.8
    private double _currentPlaceholderFontSize = 54; // 67 * 0.8
    private double _currentCardPadding = 14; // 18 * 0.8
    
    private GlobalKeyboardHandler? _keyboardHandler;
    private MainWindowViewModel? _mainWindowViewModel;
    private bool _pendingManualModeRequest = false;
    private bool _pendingClassroomModeRequest = false;
    
    public StudentGalleryView()
    {
        Log.Information("StudentGalleryView constructor started");
        InitializeComponent();
        Log.Information("StudentGalleryView InitializeComponent completed");
        
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        Log.Information("StudentGalleryView event handlers subscribed");
        
        // Subscribe to container prepared events for newly created cards
        var studentsContainer = this.FindControl<ItemsControl>("StudentsContainer");
        if (studentsContainer != null)
        {
            studentsContainer.ContainerPrepared += OnContainerPrepared;
            Log.Information("StudentsContainer found and ContainerPrepared event subscribed");
        }
        else
        {
            Log.Warning("StudentsContainer not found during constructor");
        }
        
        Log.Information("StudentGalleryView constructor completed");
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
        System.Diagnostics.Debug.WriteLine($"ViewModel_PropertyChanged: {e.PropertyName}");
        
        if (e.PropertyName == "SelectedStudent")
        {
            // Defer scrolling until layout pass completes
            Dispatcher.UIThread.Post(() => ScrollSelectedStudentIntoCenter(), DispatcherPriority.Background);
        }
        else if (e.PropertyName == "Students")
        {
            System.Diagnostics.Debug.WriteLine($"Students collection changed");
            // XAML bindings will handle visibility automatically
        }
        else if (e.PropertyName == "DisplayConfig")
        {
            // When DisplayConfig changes, update card layout with new dimensions
            Dispatcher.UIThread.Post(() => UpdateCardLayout(), DispatcherPriority.Render);
        }
        else if (e.PropertyName == "IsAddingStudent")
        {
            System.Diagnostics.Debug.WriteLine($"IsAddingStudent changed - checking view visibility");
            var addStudentView = this.FindControl<AddStudentView>("AddStudentView");
            var scrollViewer = this.FindControl<ScrollViewer>("StudentsScrollViewer");
            if (addStudentView != null && scrollViewer != null)
            {
                System.Diagnostics.Debug.WriteLine($"AddStudentView IsVisible: {addStudentView.IsVisible}, ScrollViewer IsVisible: {scrollViewer.IsVisible}");
            }
        }
        else if (e.PropertyName == "ShowMultipleStudents" || e.PropertyName == "ShowSingleStudent" || e.PropertyName == "ShowEmptyState")
        {
            System.Diagnostics.Debug.WriteLine($"View state changed: {e.PropertyName}");
            // XAML bindings will handle visibility automatically
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Log.Information("StudentGalleryView OnDataContextChanged triggered");
        Log.Information("New DataContext type: {DataType}", DataContext?.GetType().Name ?? "null");
        
        // Unsubscribe from previous ViewModel if any
        if (sender is StudentGalleryView view && view.Tag is StudentGalleryViewModel oldViewModel)
        {
            Log.Information("Unsubscribing from previous StudentGalleryViewModel");
            UnsubscribeFromViewModelSelections(oldViewModel);
            
            // Clean up keyboard handler
            _keyboardHandler?.Dispose();
            _keyboardHandler = null;
        }
        
        // Unsubscribe from previous MainWindowViewModel if any
        if (_mainWindowViewModel != null)
        {
            _mainWindowViewModel = null;
        }
        
        // Unsubscribe from StudentCoordinatorService events
        var coordinator = Services.StudentCoordinatorService.Instance;
        coordinator.StudentImageChangeRequested -= OnStudentImageChangeRequestedFromCoordinator;
        
        // Subscribe to new ViewModel
        if (DataContext is StudentGalleryViewModel viewModel)
        {
            Log.Information("New StudentGalleryViewModel received - setting up event handlers");
            
            // Store reference for cleanup
            Tag = viewModel;
            SubscribeToViewModelSelections(viewModel);
            Log.Information("Subscribed to ViewModel property changes");
            
            // Initialize keyboard handler
            var searchTextBox = this.FindControl<TextBox>("SearchTextBox");
            if (searchTextBox != null)
            {
                _keyboardHandler?.Dispose(); // Clean up previous handler
                _keyboardHandler = new GlobalKeyboardHandler(viewModel, searchTextBox, this);
                Log.Information("GlobalKeyboardHandler initialized");
            }
            else
            {
                Log.Warning("SearchTextBox not found, keyboard handler not initialized");
            }
            
            // Ensure ItemsControl container events are properly subscribed
            var studentsContainer = this.FindControl<ItemsControl>("StudentsContainer");
            if (studentsContainer != null)
            {
                studentsContainer.ContainerPrepared -= OnContainerPrepared;
                studentsContainer.ContainerPrepared += OnContainerPrepared;
                Log.Information("StudentsContainer ContainerPrepared event re-subscribed");
            }
            else
            {
                Log.Warning("StudentsContainer not found during DataContext change");
            }

            // Set up AddStudentView when it becomes visible
            SetupAddStudentView(viewModel);
            Log.Information("AddStudentView setup completed");
            
            // Subscribe to StudentCoordinatorService events for image changes
            coordinator.StudentImageChangeRequested += OnStudentImageChangeRequestedFromCoordinator;
            Log.Information("Subscribed to StudentCoordinatorService.StudentImageChangeRequested");
        }
        else
        {
            Log.Warning("DataContext is not StudentGalleryViewModel: {DataType}", DataContext?.GetType().Name ?? "null");
        }
    }

    private void SubscribeToMainWindowViewModel()
    {
        // Find the MainWindow and subscribe to its ViewModel events
        var mainWindow = TopLevel.GetTopLevel(this) as Window;
        if (mainWindow?.DataContext is MainWindowViewModel mainViewModel)
        {
            _mainWindowViewModel = mainViewModel;
        }
    }

    private void SetupAddStudentView(StudentGalleryViewModel viewModel)
    {
        // Set up the AddStudentView DataContext immediately
        SetupAddStudentViewDataContext(viewModel);
        
        // Subscribe to IsAddingStudent changes to ensure DataContext is set when needed
        viewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(StudentGalleryViewModel.IsAddingStudent) && viewModel.IsAddingStudent)
            {
                // Ensure DataContext is set when becoming visible
                SetupAddStudentViewDataContext(viewModel);
            }
        };
    }

    private void SetupAddStudentViewDataContext(StudentGalleryViewModel viewModel)
    {
        System.Diagnostics.Debug.WriteLine("SetupAddStudentViewDataContext called");
        // Find the AddStudentView and wire up its events
        var addStudentView = this.FindControl<AddStudentView>("AddStudentView");
        if (addStudentView != null)
        {
            System.Diagnostics.Debug.WriteLine($"AddStudentView found, current DataContext: {addStudentView.DataContext?.GetType().Name ?? "null"}");
            
            // Only set DataContext if it's not already an AddStudentViewModel
            if (addStudentView.DataContext is not AddStudentViewModel)
            {
                // Create AddStudentViewModel and set it as DataContext
                var addStudentViewModel = new AddStudentViewModel(viewModel.AuthService);
                addStudentViewModel.LoadOptionsFromStudents(viewModel.AllStudents);
                
                addStudentView.DataContext = addStudentViewModel;
                System.Diagnostics.Debug.WriteLine($"AddStudentView DataContext set to: {addStudentView.DataContext?.GetType().Name ?? "null"}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("AddStudentView already has correct DataContext");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("AddStudentView not found!");
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


    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Log.Information("StudentGalleryView OnLoaded triggered");
        Log.Information("ViewModel type: {ViewModelType}", ViewModel?.GetType().Name ?? "null");
        
        // Ensure we have the container prepared event subscribed after loading
        var studentsContainer = this.FindControl<ItemsControl>("StudentsContainer");
        if (studentsContainer != null)
        {
            studentsContainer.ContainerPrepared -= OnContainerPrepared; // Avoid double subscription
            studentsContainer.ContainerPrepared += OnContainerPrepared;
            Log.Information("StudentsContainer found and ContainerPrepared event subscribed in OnLoaded");
        }
        else
        {
            Log.Error("StudentsContainer not found in OnLoaded - this will cause issues!");
        }
        
        Log.Information("Calling UpdateCardLayout...");
        UpdateCardLayout();

        // Subscribe to ViewModel selection changes
        Log.Information("Subscribing to ViewModel selection changes...");
        SubscribeToViewModelSelections(ViewModel);
        
        // Subscribe to MainWindowViewModel events for mode switching (after visual tree is ready)
        Log.Information("Subscribing to MainWindowViewModel events...");
        SubscribeToMainWindowViewModel();
        
        Log.Information("StudentGalleryView OnLoaded completed");
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
                             _currentNameFontSize, _currentClassFontSize, _currentTeacherFontSize,
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
                                  double nameFontSize, double classFontSize, double teacherFontSize, 
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
        Services.StudentCoordinatorService.Instance.PublishStudentImageChangeRequested(student);
    }

    // Event handler for detailed view profile image clicks
    private async void OnDetailedProfileImageClicked(object? sender, SchoolOrganizer.Models.Student student)
    {
        Services.StudentCoordinatorService.Instance.PublishStudentImageChangeRequested(student);
    }

    // Event handler for when a profile image has been updated via ImageCropWindow
    private async void OnProfileImageUpdated(object? sender, (SchoolOrganizer.Models.Student student, string imagePath) args)
    {
        Services.StudentCoordinatorService.Instance.PublishStudentImageUpdated(args.student, args.imagePath, args.student.CropSettings, args.student.OriginalImagePath);
    }

    // Event handler for when StudentCoordinatorService requests an image change
    private async void OnStudentImageChangeRequestedFromCoordinator(object? sender, SchoolOrganizer.Models.Student student)
    {
        await OpenImageCropperForStudent(student);
    }

    /// <summary>
    /// Opens the ImageCropper window for editing the profile image.
    /// </summary>
    private async Task OpenImageCropperForStudent(Student student)
    {
        try
        {
            // Get the parent window
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow == null)
            {
                System.Diagnostics.Debug.WriteLine("StudentGalleryView: Could not find parent window");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"StudentGalleryView: Opening ImageCropper for student: {student.Name} (ID: {student.Id})");

            // Open the ImageCrop window with student context, passing existing ORIGINAL image and crop settings
            var result = await ImageCropWindow.ShowForStudentAsync(
                parentWindow,
                student.Id,
                student.OriginalImagePath,  // Load the original, not the cropped result
                student.CropSettings);

            System.Diagnostics.Debug.WriteLine($"StudentGalleryView: ImageCropper returned: imagePath={result.imagePath ?? "NULL"}, cropSettings={result.cropSettings ?? "NULL"}, original={result.originalImagePath ?? "NULL"}");

            if (!string.IsNullOrEmpty(result.imagePath))
            {
                System.Diagnostics.Debug.WriteLine($"StudentGalleryView: Image saved to: {result.imagePath}");
                System.Diagnostics.Debug.WriteLine($"StudentGalleryView: Raising ProfileImageUpdated event for student {student.Id}");

                // Use StudentCoordinatorService to publish the image update with all the crop data
                // The ViewModel will handle updating the student objects properly
                Services.StudentCoordinatorService.Instance.PublishStudentImageUpdated(student, result.imagePath, result.cropSettings, result.originalImagePath);

                System.Diagnostics.Debug.WriteLine($"StudentGalleryView: StudentCoordinatorService.PublishStudentImageUpdated called successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("StudentGalleryView: ImageCropper closed without saving (result was null or empty)");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StudentGalleryView: Error opening ImageCropper: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StudentGalleryView: Stack trace: {ex.StackTrace}");
        }
    }

    // Event handler for View Assignments button click
    private async void OnViewAssignmentsClicked(object? sender, SchoolOrganizer.Views.ProfileCards.ViewAssignmentsClickedEventArgs e)
    {
        await HandleViewAssignments(e.Student);
    }

    // Handle opening the AssignmentViewer for a student
    private async Task HandleViewAssignments(SchoolOrganizer.Models.Student student)
    {
        // Delegate to StudentCoordinatorService
        Services.StudentCoordinatorService.Instance.PublishViewAssignmentsRequested(student);
    }



    // Event handler for clicking on student cards
    private void OnStudentCardClicked(object? sender, IPerson person)
    {
        if (person is Student student)
        {
            Services.StudentCoordinatorService.Instance.PublishStudentSelected(student);
        }
    }

    // Event handler for double-clicking on student cards
    private void OnStudentCardDoubleClicked(object? sender, IPerson person)
    {
        if (person is Student student && ViewModel != null)
        {
            // Call the double-click method directly on the ViewModel
            ViewModel.DoubleClickStudentCommand.Execute(student);
        }
    }

    // Event handler for clicking on add student cards
    private async void OnAddStudentCardClicked(object? sender, IPerson person)
    {
        Services.StudentCoordinatorService.Instance.PublishAddStudentRequested();
    }

    // Event handler for double-clicking on background to return to gallery
    private void OnBackgroundDoubleTapped(object? sender, RoutedEventArgs e)
    {
        Services.StudentCoordinatorService.Instance.PublishStudentDeselected();
        // Reinitialize keyboard handler after returning from detailed view
        ReinitializeKeyboardHandler();
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
