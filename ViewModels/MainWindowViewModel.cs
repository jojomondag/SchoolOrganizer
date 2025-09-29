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

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly GoogleAuthService _authService;
    private readonly UserProfileService _userProfileService;
    private readonly StudentGalleryViewModel _studentGalleryViewModel;

    [ObservableProperty]
    private ViewModelBase currentViewModel;

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
        CurrentViewModel = _studentGalleryViewModel;
        
        // If we have an authService, we're already authenticated
        if (authService != null)
        {
            IsAuthenticated = true;
            TeacherName = _authService.TeacherName;
            Task.Run(LoadProfileImageAsync);
            Greeting = $"Welcome, {TeacherName}!";
        }
        else
        {
            // Initialize authentication
            Task.Run(InitializeAuthenticationAsync);
        }
    }

    public bool IsStudentGalleryActive => CurrentViewModel is StudentGalleryViewModel;

    public bool IsHomeActive => CurrentViewModel is HomeViewModel;

    public IBrush ActiveContentBrush => CurrentViewModel switch
    {
        StudentGalleryViewModel => Brushes.White,
        HomeViewModel => new SolidColorBrush((Color)Application.Current!.Resources["LightBlueColor"]!),
        _ => Brushes.White
    };

    partial void OnCurrentViewModelChanged(ViewModelBase value)
    {
        OnPropertyChanged(nameof(IsStudentGalleryActive));
        OnPropertyChanged(nameof(IsHomeActive));
        OnPropertyChanged(nameof(ActiveContentBrush));
    }

    [RelayCommand]
    private void NavigateToStudentGallery()
    {
        CurrentViewModel = _studentGalleryViewModel;
    }

    [RelayCommand]
    private void NavigateToHome()
    {
        CurrentViewModel = new HomeViewModel();
    }

    [RelayCommand]
    private async Task Login()
    {
        try
        {
            Greeting = "Authenticating with Google...";
            IsAuthenticated = await _authService.AuthenticateAsync();
            if (IsAuthenticated)
            {
                TeacherName = _authService.TeacherName;
                await LoadProfileImageAsync();
                Greeting = $"Welcome, {TeacherName}!";
                
                // Update StudentGalleryViewModel with authentication state
                _studentGalleryViewModel.UpdateAuthenticationState(_authService);
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

    [RelayCommand]
    private void Logout()
    {
        try
        {
            _authService.ClearCredentials();
            IsAuthenticated = false;
            TeacherName = "Unknown Teacher";
            ProfileImage = null;
            Greeting = "Welcome to School Organizer!";
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
            // Try to authenticate with existing credentials
            IsAuthenticated = await _authService.CheckAndAuthenticateAsync();
            if (IsAuthenticated)
            {
                TeacherName = _authService.TeacherName;
                await LoadProfileImageAsync();
                Greeting = $"Welcome back, {TeacherName}!";
                
                // Update StudentGalleryViewModel with authentication state
                _studentGalleryViewModel.UpdateAuthenticationState(_authService);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during authentication initialization");
        }
    }

    private async Task LoadProfileImageAsync()
    {
        try
        {
            var (profileImage, statusMessage) = await _userProfileService.LoadProfileImageAsync();
            ProfileImage = profileImage;
            
            // Also set the profile image in StudentGalleryViewModel
            _studentGalleryViewModel.SetProfileImage(profileImage);
            
            // Logging is handled by UserProfileService
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading profile image");
        }
    }
}

// Placeholder for future home view
public class HomeViewModel : ViewModelBase
{
    public string Title { get; } = "Home";
    public string Message { get; } = "Welcome to the School Organizer application!";
}
