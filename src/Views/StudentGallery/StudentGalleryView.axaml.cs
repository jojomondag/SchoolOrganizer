using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchoolOrganizer.Src.ViewModels;
using System.Threading.Tasks;
using Avalonia.Threading;
using SchoolOrganizer.Src.Services;
using SchoolOrganizer.Src.Models.Students;
using Serilog;


namespace SchoolOrganizer.Src.Views.StudentGallery;

public partial class StudentGalleryView : UserControl
{
    public StudentGalleryViewModel? ViewModel => DataContext as StudentGalleryViewModel;

    private GlobalKeyboardHandler? _keyboardHandler;
    
    public StudentGalleryView()
    {
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
    }

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        // Cards use XAML template styling - no dynamic updates needed
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateCardLayout();
        
        // Update display level based on new window size
        if (ViewModel != null)
        {
            ViewModel.OnWindowResized();
        }
    }

    private void UpdateCardLayout()
    {
        // Cards use XAML template styling - no dynamic layout updates needed
    }

    // Event handler for when StudentCoordinatorService requests an image change
    private async void OnStudentImageChangeRequestedFromCoordinator(object? sender, SchoolOrganizer.Src.Models.Students.Student student)
    {
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
        await SaveCurrentCropStateAsync();
    }

    /// <summary>
    /// Opens the ImageCropper view for editing the profile image.
    /// </summary>
    private async Task OpenImageCropperForStudent(Student student)
    {
        try
        {
            if (ViewModel == null) return;

            // Set up the image editing state in the ViewModel
            ViewModel.StartImageEdit(student, student.OriginalImagePath, student.CropSettings);

            // Initialize the ImageCropView
            var imageCropContainer = this.FindControl<Border>("ImageCropViewContainer");
            
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

                // Load the image for the student with fallback to PictureUrl when OriginalImagePath is missing
                var originalPath = string.IsNullOrWhiteSpace(student.OriginalImagePath) && !string.IsNullOrWhiteSpace(student.PictureUrl) && System.IO.File.Exists(student.PictureUrl)
                    ? student.PictureUrl
                    : student.OriginalImagePath;
                await imageCropView.LoadImageForStudentAsync(
                    student.Id,
                    originalPath,
                    student.CropSettings
                );
            }
        }
        catch (Exception ex)
        {
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
        Services.StudentCoordinatorService.Instance.PublishAddStudentRequested();
    }

    // Event handler for double-clicking on background to return to gallery
    private void OnBackgroundDoubleTapped(object? sender, RoutedEventArgs e)
    {
        Services.StudentCoordinatorService.Instance.PublishStudentDeselected();
        // Reinitialize keyboard handler after returning from detailed view
        ReinitializeKeyboardHandler();
    }
}
