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

    private ObservableObject _currentViewModel;

    public ObservableObject CurrentViewModel
    {
        get => _currentViewModel;
        set
        {
        if (SetProperty(ref _currentViewModel, value))
        {
            OnPropertyChanged(nameof(IsStudentGalleryActive));
            OnPropertyChanged(nameof(IsClassroomDownloadActive));
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
        
        // Subscribe to StudentCoordinatorService for add-student mode detection
        var coordinator = Services.StudentCoordinatorService.Instance;
        coordinator.AddStudentRequested += OnCoordinatorAddStudentRequested;
        coordinator.AddStudentCompleted += OnCoordinatorAddStudentCompleted;
        coordinator.AddStudentCancelled += OnCoordinatorAddStudentCancelled;
        
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
        System.Diagnostics.Debug.WriteLine("MainWindowViewModel: OnCoordinatorAddStudentRequested called");
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
    public bool IsAddStudentMode => IsStudentGalleryActive && _studentGalleryViewModel.IsAddingStudent;
    public bool IsInCropMode => IsStudentGalleryActive && _studentGalleryViewModel.IsEditingImage;
    public IBrush ActiveContentBrush => CurrentViewModel switch
    {
        StudentGalleryViewModel => Brushes.White,
        ClassroomDownloadViewModel => Brushes.White,
        _ => Brushes.White
    };

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
        
        CurrentViewModel = _studentGalleryViewModel;
        
        Log.Information("Current ViewModel after navigation: {CurrentViewModel}", CurrentViewModel?.GetType().Name ?? "null");
    }

    [RelayCommand]
    private void NavigateToManualEntry()
    {
        if (IsAddStudentMode)
        {
            // Use StudentCoordinatorService to publish manual entry request
            Services.StudentCoordinatorService.Instance.PublishManualEntryRequested();
        }
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
            _classroomDownloadViewModel = new ClassroomDownloadViewModel(_authService);
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
        System.Diagnostics.Debug.WriteLine("MainWindowViewModel: AddStudent command called");
        _studentGalleryViewModel.AddStudentCommand.Execute(null);
        OnPropertyChanged(nameof(IsAddStudentMode));
    }

    [RelayCommand]
    private void ToggleMenu() => IsMenuOpen = !IsMenuOpen;

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
            IsAuthenticated = await _authService.CheckAndAuthenticateAsync();
            if (IsAuthenticated)
            {
                SetAuthenticatedState("Welcome back");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during authentication initialization");
        }
    }

    private async Task AuthenticateAsync(string loadingMessage, bool isInitialLoad)
    {
        try
        {
            Greeting = loadingMessage;
            IsAuthenticated = await _authService.AuthenticateAsync();
            
            if (IsAuthenticated)
            {
                SetAuthenticatedState(isInitialLoad ? "Welcome" : "Welcome");
            }
            else
            {
                Greeting = "Authentication failed. Please check your credentials.json file in the Resources folder.";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during authentication");
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
