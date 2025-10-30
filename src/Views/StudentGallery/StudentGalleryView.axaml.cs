using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchoolOrganizer.Src.ViewModels;
using System.Threading.Tasks;
using System.Linq;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Threading;
using SchoolOrganizer.Src.Services;
using SchoolOrganizer.Src.Models.Students;
using Serilog;


namespace SchoolOrganizer.Src.Views.StudentGallery;

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
    
    public StudentGalleryView()
    {
        System.Diagnostics.Debug.WriteLine("StudentGalleryView: Constructor called");
        InitializeComponent();
        
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        
        // Subscribe to crop save requests from the ViewModel
        OnDataContextChanged(this, EventArgs.Empty);
        
        // Subscribe to container prepared events for newly created cards
        var studentsContainer = this.FindControl<ItemsControl>("StudentsContainer");
        if (studentsContainer != null)
        {
            studentsContainer.ContainerPrepared += OnContainerPrepared;
        }
        else
        {
            Log.Warning("StudentsContainer not found during constructor");
        }
        System.Diagnostics.Debug.WriteLine("StudentGalleryView: Constructor completed");
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
    
        if (e.PropertyName == "DisplayConfig")
        {
            // When DisplayConfig changes, update card layout with new dimensions
            Dispatcher.UIThread.Post(() => UpdateCardLayout(), DispatcherPriority.Render);
        }
        else if (e.PropertyName == "IsAddingStudent")
        {
            var addStudentView = this.FindControl<AddStudentView>("AddStudentView");
            var scrollViewer = this.FindControl<ScrollViewer>("StudentsScrollViewer");
            if (addStudentView != null && scrollViewer != null)
            {
            }
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from previous ViewModel if any
        if (sender is StudentGalleryView view && view.Tag is StudentGalleryViewModel oldViewModel)
        {
            UnsubscribeFromViewModelSelections(oldViewModel);
            
            // Unsubscribe from crop save requests
            oldViewModel.CropSaveRequested -= OnCropSaveRequested;
            
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
        coordinator.EditStudentRequested -= OnEditStudentRequestedFromCoordinator;
        
        // Subscribe to new ViewModel
        if (DataContext is StudentGalleryViewModel viewModel)
        {
            // Store reference for cleanup
            Tag = viewModel;
            SubscribeToViewModelSelections(viewModel);
            
            // Initialize keyboard handler
            var searchTextBox = this.FindControl<TextBox>("SearchTextBox");
            if (searchTextBox != null)
            {
                _keyboardHandler?.Dispose(); // Clean up previous handler
                _keyboardHandler = new GlobalKeyboardHandler(viewModel, searchTextBox, this);
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
            }
            else
            {
                Log.Warning("StudentsContainer not found during DataContext change");
            }

            // Set up AddStudentView when it becomes visible
            SetupAddStudentView(viewModel);
            
            // Subscribe to StudentCoordinatorService events for image changes
            coordinator.StudentImageChangeRequested += OnStudentImageChangeRequestedFromCoordinator;
            coordinator.EditStudentRequested += OnEditStudentRequestedFromCoordinator;
            
            // Subscribe to crop save requests
            viewModel.CropSaveRequested += OnCropSaveRequested;
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
            else if (e.PropertyName == nameof(StudentGalleryViewModel.IsAuthenticated) && viewModel.IsAuthenticated)
            {
                // When authentication completes, update the AddStudentView's ViewModel with auth service
                UpdateAddStudentViewAuthService(viewModel);
            }
        };
    }

    private void SetupAddStudentViewDataContext(StudentGalleryViewModel viewModel)
    {
        // Find the AddStudentView and wire up its events
        var addStudentView = this.FindControl<AddStudentView>("AddStudentView");
        if (addStudentView != null)
        {
            
            // Only set DataContext if it's not already an AddStudentViewModel
            if (addStudentView.DataContext is not AddStudentViewModel)
            {
                // Create AddStudentViewModel and set it as DataContext
                var addStudentViewModel = new AddStudentViewModel(viewModel.AuthService);
                addStudentViewModel.LoadOptionsFromStudents(viewModel.AllStudents);
                
                addStudentView.DataContext = addStudentViewModel;
            }

            // If we're in edit mode, initialize the AddStudentView for editing
            if (viewModel.StudentBeingEdited != null && addStudentView.DataContext is AddStudentViewModel editViewModel)
            {
                // Load available options from existing students
                editViewModel.LoadOptionsFromStudents(viewModel.AllStudents);
                
                // Initialize the AddStudentViewModel for editing this student
                editViewModel.InitializeForEdit(viewModel.StudentBeingEdited);
            }
            else if (addStudentView.DataContext is AddStudentViewModel addViewModel)
            {
                // Reset to add mode for new students
                addViewModel.ResetToAddMode();
            }
        }
    }

    private void UpdateAddStudentViewAuthService(StudentGalleryViewModel viewModel)
    {
        // Find the AddStudentView and update its ViewModel with auth service
        var addStudentView = this.FindControl<AddStudentView>("AddStudentView");
        if (addStudentView?.DataContext is AddStudentViewModel addStudentViewModel && viewModel.AuthService != null)
        {
            addStudentViewModel.UpdateAuthService(viewModel.AuthService);
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
            }
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
        else
        {
            Log.Error("StudentsContainer not found in OnLoaded - this will cause issues!");
        }
        
        UpdateCardLayout();

        // Subscribe to ViewModel selection changes
        SubscribeToViewModelSelections(ViewModel);
        
        // Subscribe to MainWindowViewModel events for mode switching (after visual tree is ready)
        SubscribeToMainWindowViewModel();
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
        // Don't auto-scroll on size changes - let user control scrolling manually
        // Dispatcher.UIThread.Post(() => ScrollSelectedStudentIntoCenter(), DispatcherPriority.Background);
        
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


    // Event handler for when a profile image has been updated via ImageCropWindow
    private void OnProfileImageUpdated(object? sender, (SchoolOrganizer.Src.Models.Students.Student student, string imagePath) args)
    {
        Services.StudentCoordinatorService.Instance.PublishStudentImageUpdated(args.student, args.imagePath, args.student.CropSettings, args.student.OriginalImagePath);
    }

    // Event handler for when StudentCoordinatorService requests an image change
    private async void OnStudentImageChangeRequestedFromCoordinator(object? sender, SchoolOrganizer.Src.Models.Students.Student student)
    {
        System.Diagnostics.Debug.WriteLine($"OnStudentImageChangeRequestedFromCoordinator called for student: {student.Name}");
        System.Diagnostics.Debug.WriteLine($"Student OriginalImagePath: {student.OriginalImagePath}");
        System.Diagnostics.Debug.WriteLine($"Student CropSettings: {student.CropSettings}");
        await OpenImageCropperForStudent(student);
    }

    // Event handler for when StudentCoordinatorService requests editing a student
    private async void OnEditStudentRequestedFromCoordinator(object? sender, SchoolOrganizer.Src.Models.Students.Student student)
    {
        await OpenEditStudentWindow(student);
    }

    // Event handler for when crop save is requested (e.g., during navigation)
    private async void OnCropSaveRequested(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("OnCropSaveRequested called");
        await SaveCurrentCropStateAsync();
    }

    /// <summary>
    /// Opens the ImageCropper view for editing the profile image.
    /// </summary>
    private async Task OpenImageCropperForStudent(Student student)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"OpenImageCropperForStudent called for student: {student.Name}");
            System.Diagnostics.Debug.WriteLine($"ViewModel is null: {ViewModel == null}");
            
            if (ViewModel == null) return;

            // Set up the image editing state in the ViewModel
            ViewModel.StartImageEdit(student, student.OriginalImagePath, student.CropSettings);
            System.Diagnostics.Debug.WriteLine($"IsEditingImage set to: {ViewModel.IsEditingImage}");

            // Initialize the ImageCropView
            var imageCropContainer = this.FindControl<Border>("ImageCropViewContainer");
            System.Diagnostics.Debug.WriteLine($"ImageCropViewContainer found: {imageCropContainer != null}");
            
            if (imageCropContainer != null)
            {
                // Create and setup the ImageCropView
                var imageCropView = new SchoolOrganizer.Src.Views.ImageCrop.ImageCropView
                {
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
                };
                var imageCropViewModel = new ImageCropViewModel();
                imageCropView.DataContext = imageCropViewModel;

                // Wire up events
                imageCropView.ImageSaved += OnImageCropSaved;
                imageCropView.CancelRequested += OnImageCropCancelled;
                imageCropViewModel.CancelRequested += OnImageCropCancelled;
                imageCropViewModel.ImageSaved += OnImageCropViewModelSaved;

                // Add the view to the container
                imageCropContainer.Child = imageCropView;
                System.Diagnostics.Debug.WriteLine($"ImageCropView added to container");

                // Load the image for the student with fallback to PictureUrl when OriginalImagePath is missing
                var originalPath = string.IsNullOrWhiteSpace(student.OriginalImagePath) && !string.IsNullOrWhiteSpace(student.PictureUrl) && System.IO.File.Exists(student.PictureUrl)
                    ? student.PictureUrl
                    : student.OriginalImagePath;
                System.Diagnostics.Debug.WriteLine($"Calling LoadImageForStudentAsync with studentId: {student.Id}, originalPath: {originalPath}");
                await imageCropView.LoadImageForStudentAsync(
                    student.Id,
                    originalPath,
                    student.CropSettings
                );
                System.Diagnostics.Debug.WriteLine($"LoadImageForStudentAsync completed");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OpenImageCropperForStudent: {ex.Message}");
            Log.Error(ex, "Error opening ImageCropper");
        }
    }

    private void OnImageCropSaved(object? sender, string imagePath)
    {
        if (ViewModel == null || ViewModel.StudentForImageEdit == null) return;

        var student = ViewModel.StudentForImageEdit;

        // Check if this is a temporary student (ID = -1) from AddStudentView
        if (student.Id == -1)
        {
            // For temporary students, publish the update with the original student object
            Services.StudentCoordinatorService.Instance.PublishStudentImageUpdated(
                student, imagePath, student.CropSettings, student.OriginalImagePath);
        }
        else
        {
            // For regular students, the StudentGalleryViewModel will handle the update via CompleteImageEdit
            // Just need to trigger the save in ViewModel which will be handled by the ImageSaved event
        }
    }

    private void OnImageCropViewModelSaved(object? sender, (string imagePath, string? cropSettings, string? originalImagePath) data)
    {
        if (ViewModel == null || ViewModel.StudentForImageEdit == null) return;

        var student = ViewModel.StudentForImageEdit;

        // Check if this is a temporary student (ID = -1) from AddStudentView
        if (student.Id == -1)
        {
            // For temporary students, publish through coordinator so AddStudentView can receive it
            Services.StudentCoordinatorService.Instance.PublishStudentImageUpdated(
                student, data.imagePath, data.cropSettings, data.originalImagePath);

            // Cancel the image edit state (don't save to JSON for temporary students)
            ViewModel.CancelImageEditCommand.Execute(null);
        }
        else
        {
            // For regular students, use the ViewModel's CompleteImageEdit command to save
            _ = ViewModel.CompleteImageEditCommand.ExecuteAsync(data);
        }

        // Clean up the view
        var imageCropContainer = this.FindControl<Border>("ImageCropViewContainer");
        if (imageCropContainer != null)
        {
            if (imageCropContainer.Child is SchoolOrganizer.Src.Views.ImageCrop.ImageCropView view)
            {
                view.ImageSaved -= OnImageCropSaved;
                view.CancelRequested -= OnImageCropCancelled;
                if (view.ViewModel != null)
                {
                    view.ViewModel.CancelRequested -= OnImageCropCancelled;
                    view.ViewModel.ImageSaved -= OnImageCropViewModelSaved;
                }
            }
            imageCropContainer.Child = null;
        }
    }

    private void OnImageCropCancelled(object? sender, EventArgs e)
    {
        if (ViewModel == null) return;

        // Cancel the image edit
        ViewModel.CancelImageEditCommand.Execute(null);

        // Clean up the view
        var imageCropContainer = this.FindControl<Border>("ImageCropViewContainer");
        if (imageCropContainer != null)
        {
            if (imageCropContainer.Child is SchoolOrganizer.Src.Views.ImageCrop.ImageCropView view)
            {
                view.ImageSaved -= OnImageCropSaved;
                view.CancelRequested -= OnImageCropCancelled;
                if (view.ViewModel != null)
                {
                    view.ViewModel.CancelRequested -= OnImageCropCancelled;
                    view.ViewModel.ImageSaved -= OnImageCropViewModelSaved;
                }
            }
            imageCropContainer.Child = null;
        }
    }

    /// <summary>
    /// Saves the current crop state - called when navigating away from crop mode
    /// </summary>
    public async Task SaveCurrentCropStateAsync()
    {
        if (ViewModel == null || !ViewModel.IsEditingImage) return;

        try
        {
            System.Diagnostics.Debug.WriteLine("StudentGalleryView: SaveCurrentCropStateAsync called");
            
            // Find the ImageCropView and trigger its save functionality
            var imageCropContainer = this.FindControl<Border>("ImageCropViewContainer");
            if (imageCropContainer?.Child is SchoolOrganizer.Src.Views.ImageCrop.ImageCropView imageCropView)
            {
                // Trigger the save functionality in the ImageCropView
                // This will call HandleSaveButtonAsync which saves the image and triggers the completion
                await imageCropView.TriggerSaveAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in SaveCurrentCropStateAsync: {ex.Message}");
            Log.Error(ex, "Error saving crop state during navigation");
        }
    }

    /// <summary>
    /// Opens the in-view AddStudentView in edit mode for editing student details.
    /// </summary>
    private Task OpenEditStudentWindow(Student student)
    {
        try
        {
            // Set the student being edited in the ViewModel for the AddStudentView to use
            if (ViewModel != null)
            {
                // Store the student being edited in a way the AddStudentView can access it
                // We'll use the existing AddStudent functionality but modify it for editing
                ViewModel.SetStudentForEdit(student);
                
                // Trigger the AddStudent mode to show the AddStudentView
                ViewModel.AddStudentCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening in-view edit");
        }
        return Task.CompletedTask;
    }

    // Event handler for View Assignments button click
    private void OnViewAssignmentsClicked(object? sender, SchoolOrganizer.Src.Views.ProfileCards.ViewAssignmentsClickedEventArgs e)
    {
        HandleViewAssignments(e.Student);
    }

    // Handle opening the AssignmentViewer for a student
    private Task HandleViewAssignments(SchoolOrganizer.Src.Models.Students.Student student)
    {
        // Delegate to StudentCoordinatorService
        Services.StudentCoordinatorService.Instance.PublishViewAssignmentsRequested(student);
        return Task.CompletedTask;
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
    private void OnAddStudentCardClicked(object? sender, IPerson person)
    {
        System.Diagnostics.Debug.WriteLine("StudentGalleryView: OnAddStudentCardClicked called");
        System.Diagnostics.Debug.WriteLine($"StudentGalleryView: Sender type: {sender?.GetType().Name}");
        System.Diagnostics.Debug.WriteLine($"StudentGalleryView: Person type: {person?.GetType().Name}");
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
                    if (i is SchoolOrganizer.Src.Models.Students.Student s)
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
            Log.Error(ex, "Error scrolling selected student into center");
        }
        }
}
