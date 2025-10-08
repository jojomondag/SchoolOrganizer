using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolOrganizer.Services;
using Serilog;

namespace SchoolOrganizer.ViewModels;

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
                OnPropertyChanged(nameof(IsHomeActive));
                OnPropertyChanged(nameof(IsClassroomDownloadActive));
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

    public MainWindowViewModel(GoogleAuthService? authService = null)
    {
        _authService = authService ?? new GoogleAuthService();
        _userProfileService = new UserProfileService(_authService);
        _studentGalleryViewModel = new StudentGalleryViewModel(authService);
        _currentViewModel = _studentGalleryViewModel;
        
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

    public bool IsStudentGalleryActive => CurrentViewModel is StudentGalleryViewModel;
    public bool IsHomeActive => CurrentViewModel is HomeViewModel;
    public bool IsClassroomDownloadActive => CurrentViewModel is ClassroomDownloadViewModel;
    public IBrush ActiveContentBrush => CurrentViewModel switch
    {
        StudentGalleryViewModel => Brushes.White,
        HomeViewModel => (IBrush)Application.Current!.Resources["LightBlueBrush"]!,
        ClassroomDownloadViewModel => Brushes.White,
        _ => Brushes.White
    };

    [RelayCommand]
    private void NavigateToStudentGallery() => CurrentViewModel = _studentGalleryViewModel;

    [RelayCommand]
    private void NavigateToHome() => CurrentViewModel = new HomeViewModel();

    [RelayCommand]
    private void NavigateToClassroomDownload()
    {
        if (_classroomDownloadViewModel == null)
        {
            _classroomDownloadViewModel = new ClassroomDownloadViewModel(_authService);
        }
        CurrentViewModel = _classroomDownloadViewModel;
    }

    [RelayCommand]
    private async Task Login() => await AuthenticateAsync("Authenticating with Google...", false);

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

public class HomeViewModel : ObservableObject
{
    public string Title { get; } = "Home";
    public string Message { get; } = "Welcome to the School Organizer application!";
}
