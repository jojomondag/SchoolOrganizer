using System;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolOrganizer.Src.Services;
using Serilog;

namespace SchoolOrganizer.Src.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly GoogleAuthService _authService;
    private readonly UserProfileService _userProfileService;
    private readonly StudentGalleryViewModel _studentGalleryViewModel;
    private ClassroomDownloadViewModel? _classroomDownloadViewModel;
    private readonly AssignmentViewCoordinator _assignmentCoordinator;

    private object _currentViewModel;

    public object CurrentViewModel
    {
        get => _currentViewModel;
        set
        {
            if (_currentViewModel != value)
            {
                _currentViewModel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStudentGalleryActive));
                OnPropertyChanged(nameof(IsClassroomDownloadActive));
                OnPropertyChanged(nameof(IsAssignmentViewActive));
                OnPropertyChanged(nameof(IsInCropMode));
                OnPropertyChanged(nameof(ActiveContentBrush));
            }
        }
    }

    [ObservableProperty]
    private string greeting = "Welcome to School Organizer!";

    [ObservableProperty]
    private Bitmap? profileImage;

    [ObservableProperty]
    private string teacherName = "Unknown Teacher";

    [ObservableProperty]
    private bool isAuthenticated = false;

    [ObservableProperty]
    private bool isMenuOpen = true;

    // Public property to access the AuthService
    public GoogleAuthService AuthService => _authService;

    public MainWindowViewModel(GoogleAuthService? authService = null)
    {
        _authService = authService ?? new GoogleAuthService();
        _userProfileService = new UserProfileService(_authService);
        _studentGalleryViewModel = new StudentGalleryViewModel(authService);
        _currentViewModel = _studentGalleryViewModel;
        _assignmentCoordinator = AssignmentViewCoordinator.Instance;
        
        // Subscribe to StudentCoordinatorService for add-student mode detection
        var coordinator = Services.StudentCoordinatorService.Instance;
        coordinator.AddStudentRequested += OnCoordinatorAddStudentRequested;
        coordinator.AddStudentCompleted += OnCoordinatorAddStudentCompleted;
        coordinator.AddStudentCancelled += OnCoordinatorAddStudentCancelled;
        
        // Subscribe to AssignmentViewCoordinator events
        _assignmentCoordinator.ViewModeChanged += OnAssignmentViewModeChanged;
        _assignmentCoordinator.EmbeddedViewRequested += OnEmbeddedAssignmentViewRequested;
        _assignmentCoordinator.EmbeddedViewCloseRequested += OnEmbeddedAssignmentViewCloseRequested;
        
        if (authService != null)
        {
            SetAuthenticatedState();
        }
        else
        {
            // Don't use Task.Run here to avoid threading issues
            _ = InitializeAuthenticationAsync();
        }
    }

    private void OnCoordinatorAddStudentRequested(object? sender, EventArgs e)
    {
        _studentGalleryViewModel.AddStudentCommand.Execute(null);
        OnPropertyChanged(nameof(IsAddStudentMode));
        OnPropertyChanged(nameof(IsInCropMode));
    }

    private void OnCoordinatorAddStudentCompleted(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsAddStudentMode));
        OnPropertyChanged(nameof(IsInCropMode));
    }

    private void OnCoordinatorAddStudentCancelled(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsAddStudentMode));
        OnPropertyChanged(nameof(IsInCropMode));
    }

    public bool IsStudentGalleryActive => CurrentViewModel is StudentGalleryViewModel;
    public bool IsClassroomDownloadActive => CurrentViewModel is ClassroomDownloadViewModel;
    public bool IsAssignmentViewActive => CurrentViewModel?.GetType().Name == nameof(StudentDetailViewModel);
    public bool IsAddStudentMode => IsStudentGalleryActive && _studentGalleryViewModel.IsAddingStudent;
    public bool IsInCropMode => IsStudentGalleryActive && _studentGalleryViewModel.IsEditingImage;
    public IBrush ActiveContentBrush => CurrentViewModel switch
    {
        StudentGalleryViewModel => Brushes.White,
        ClassroomDownloadViewModel => Brushes.White,
        _ when CurrentViewModel?.GetType().Name == nameof(StudentDetailViewModel) => Brushes.White,
        _ => Brushes.White
    };

    /// <summary>
    /// Gets the shared assignment view model from the coordinator
    /// </summary>
    public StudentDetailViewModel AssignmentViewModel => _assignmentCoordinator.SharedViewModel;

    [RelayCommand]
    private async Task NavigateToStudentGallery() 
    {
        // Save crop state if we're currently in crop mode
        if (IsInCropMode)
        {
            await SaveCurrentCropStateAsync();
        }

        Log.Information("NavigateToStudentGallery command executed");
        Log.Information("Current ViewModel before navigation: {CurrentViewModel}", CurrentViewModel?.GetType().Name ?? "null");
        Log.Information("StudentGalleryViewModel state - IsLoading: {IsLoading}, Students.Count: {StudentsCount}, AllStudents.Count: {AllStudentsCount}", 
            _studentGalleryViewModel.IsLoading, 
            _studentGalleryViewModel.Students.Count, 
            _studentGalleryViewModel.AllStudents.Count);
        Log.Information("StudentGalleryViewModel view properties - ShowMultipleStudents: {ShowMultipleStudents}, ShowSingleStudent: {ShowSingleStudent}, ShowEmptyState: {ShowEmptyState}", 
            _studentGalleryViewModel.ShowMultipleStudents, 
            _studentGalleryViewModel.ShowSingleStudent, 
            _studentGalleryViewModel.ShowEmptyState);
        // If a single student (fullscreen card) is shown, exit to gallery grid
        try
        {
            _studentGalleryViewModel.BackToGalleryCommand.Execute(null);
        }
        catch { }

        CurrentViewModel = _studentGalleryViewModel;
        
        Log.Information("Current ViewModel after navigation: {CurrentViewModel}", CurrentViewModel?.GetType().Name ?? "null");
    }

    [RelayCommand]
    private void NavigateToClassroomImport()
    {
        if (IsAddStudentMode)
        {
            // Use StudentCoordinatorService to publish classroom import request
            Services.StudentCoordinatorService.Instance.PublishClassroomImportRequested();
        }
    }

    [RelayCommand]
    private async Task NavigateToClassroomDownload()
    {
        // Save crop state if we're currently in crop mode
        if (IsInCropMode)
        {
            await SaveCurrentCropStateAsync();
        }

        if (_classroomDownloadViewModel == null)
        {
            _classroomDownloadViewModel = new ClassroomDownloadViewModel(_authService, _studentGalleryViewModel);
        }
        else
        {
            // Reset to classroom list view when tab is clicked
            _classroomDownloadViewModel.ResetToClassroomList();
        }
        CurrentViewModel = _classroomDownloadViewModel;
    }

    [RelayCommand]
    private async Task Login() => await AuthenticateAsync("Authenticating with Google...", false);

    [RelayCommand]
    public void AddStudent()
    {
        _studentGalleryViewModel.AddStudentCommand.Execute(null);
        OnPropertyChanged(nameof(IsAddStudentMode));
    }

    [RelayCommand]
    private void ToggleMenu() => IsMenuOpen = !IsMenuOpen;

    [RelayCommand]
    private void NavigateToAssignments()
    {
        Log.Information("NavigateToAssignments command executed");
        
        // Activate embedded view in coordinator
        _assignmentCoordinator.ActivateEmbeddedView();
        
        // Set the assignment view model as the current view
        CurrentViewModel = AssignmentViewModel;
        
        Log.Information("Navigated to embedded assignments view");
    }

    [RelayCommand]
    private void ScrollToAssignment(string assignmentName)
    {
        Log.Information("ScrollToAssignment command executed for: {AssignmentName}", assignmentName);
        
        // Raise an event or use a service to notify the EmbeddedAssignmentView to scroll
        // For now, we'll use a simple approach through the coordinator
        AssignmentViewScrollRequested?.Invoke(this, assignmentName);
    }

    /// <summary>
    /// Event raised when scrolling to a specific assignment is requested
    /// </summary>
    public event EventHandler<string>? AssignmentViewScrollRequested;

    /// <summary>
    /// Handles assignment view mode changes from the coordinator
    /// </summary>
    private void OnAssignmentViewModeChanged(object? sender, EventArgs e)
    {
        Log.Information("Assignment view mode changed - Embedded: {IsEmbedded}, Detached: {IsDetached}", 
            _assignmentCoordinator.IsEmbedded, _assignmentCoordinator.IsDetached);
        
        OnPropertyChanged(nameof(IsAssignmentViewActive));
    }

    /// <summary>
    /// Handles request to show embedded assignment view
    /// </summary>
    private void OnEmbeddedAssignmentViewRequested(object? sender, EventArgs e)
    {
        Log.Information("Embedded assignment view requested");
        CurrentViewModel = AssignmentViewModel;
    }

    /// <summary>
    /// Handles request to close embedded assignment view
    /// </summary>
    private void OnEmbeddedAssignmentViewCloseRequested(object? sender, EventArgs e)
    {
        Log.Information("Embedded assignment view close requested");
        
        // Navigate back to student gallery if we're showing the assignment view
        if (IsAssignmentViewActive)
        {
            CurrentViewModel = _studentGalleryViewModel;
        }
    }

    /// <summary>
    /// Saves the current crop state if we're in crop mode
    /// </summary>
    private async Task SaveCurrentCropStateAsync()
    {
        if (!IsInCropMode) return;

        try
        {
            // Trigger the save functionality in the StudentGalleryView
            // This will be handled by the StudentGalleryView which has access to the ImageCropView
            await _studentGalleryViewModel.SaveCurrentCropStateAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving crop state during navigation");
        }
    }

    [RelayCommand]
    private void Logout()
    {
        try
        {
            _authService.ClearCredentials();
            ResetToUnauthenticatedState();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during logout");
        }
    }

    private async Task InitializeAuthenticationAsync()
    {
        try
        {
            bool wasAuthenticated = await _authService.CheckAndAuthenticateAsync();
            if (wasAuthenticated)
            {
                SetAuthenticatedState("Welcome back");
            }
            else
            {
                // Ensure we're in unauthenticated state
                ResetToUnauthenticatedState();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during authentication initialization");
            ResetToUnauthenticatedState();
        }
    }

    private async Task AuthenticateAsync(string loadingMessage, bool isInitialLoad)
    {
        try
        {
            Greeting = loadingMessage;
            bool authenticated = await _authService.AuthenticateAsync();
            
            if (authenticated)
            {
                SetAuthenticatedState(isInitialLoad ? "Welcome" : "Welcome");
            }
            else
            {
                ResetToUnauthenticatedState();
                Greeting = "Authentication failed. Please check your credentials.json file in the Resources folder.";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during authentication");
            ResetToUnauthenticatedState();
            Greeting = $"Authentication error: {ex.Message}";
        }
    }

    private void SetAuthenticatedState(string welcomePrefix = "Welcome")
    {
        IsAuthenticated = true;
        TeacherName = _authService.TeacherName;
        Greeting = $"{welcomePrefix}, {TeacherName}!";
        Task.Run(LoadProfileImageAsync);
        _studentGalleryViewModel.UpdateAuthenticationState(_authService);
    }

    private void ResetToUnauthenticatedState()
    {
        IsAuthenticated = false;
        TeacherName = "Unknown Teacher";
        ProfileImage = null;
        Greeting = "Welcome to School Organizer!";
        // Update student gallery view model authentication state
        _studentGalleryViewModel?.UpdateAuthenticationState(null);
    }

    private async Task LoadProfileImageAsync()
    {
        try
        {
            var (profileImage, _) = await _userProfileService.LoadProfileImageAsync();
            ProfileImage = profileImage;
            _studentGalleryViewModel.SetProfileImage(profileImage);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading profile image");
        }
    }
}
