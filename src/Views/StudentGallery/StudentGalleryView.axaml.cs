using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchoolOrganizer.Src.ViewModels;
using System.Threading.Tasks;
using System.Linq;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Threading;
using SchoolOrganizer.Src.Views.Windows.ImageCrop;
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
        // Handle any necessary updates when properties change
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from previous ViewModel if any
        if (sender is StudentGalleryView view && view.Tag is StudentGalleryViewModel oldViewModel)
        {
            UnsubscribeFromViewModelSelections(oldViewModel);
            
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

            // Set up AddStudentView when it becomes visible
            SetupAddStudentView(viewModel);
            
            // Subscribe to StudentCoordinatorService events for image changes
            coordinator.StudentImageChangeRequested += OnStudentImageChangeRequestedFromCoordinator;
            coordinator.EditStudentRequested += OnEditStudentRequestedFromCoordinator;
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
        // Subscribe to ViewModel selection changes
        SubscribeToViewModelSelections(ViewModel);
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // Update display level based on new window size
        if (ViewModel != null)
        {
            ViewModel.OnWindowResized();
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
        await OpenImageCropperForStudent(student);
    }

    // Event handler for when StudentCoordinatorService requests editing a student
    private async void OnEditStudentRequestedFromCoordinator(object? sender, SchoolOrganizer.Src.Models.Students.Student student)
    {
        await OpenEditStudentWindow(student);
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
                return;
            }

            // Open the ImageCrop window with student context, passing existing ORIGINAL image and crop settings
            var result = await ImageCropWindow.ShowForStudentAsync(
                parentWindow,
                student.Id,
                student.OriginalImagePath,  // Load the original, not the cropped result
                student.CropSettings);

            if (!string.IsNullOrEmpty(result.imagePath))
            {
                // Find the fresh student object by ID from the ViewModel to avoid stale references
                var viewModel = DataContext as StudentGalleryViewModel;
                var freshStudent = viewModel?.Students.OfType<Student>().FirstOrDefault(s => s.Id == student.Id);
                
                if (freshStudent != null)
                {
                    // Use StudentCoordinatorService to publish the image update with all the crop data
                    // The ViewModel will handle updating the student objects properly
                    Services.StudentCoordinatorService.Instance.PublishStudentImageUpdated(freshStudent, result.imagePath, result.cropSettings, result.originalImagePath);
                }
                else
                {
                    Log.Warning("Could not find fresh student object with ID {StudentId} for image update", student.Id);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening ImageCropper");
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