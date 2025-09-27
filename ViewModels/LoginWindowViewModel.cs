using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolOrganizer.Services;
using Serilog;

namespace SchoolOrganizer.ViewModels;

public partial class LoginWindowViewModel : ViewModelBase
{
    private readonly GoogleAuthService _authService;

    [ObservableProperty]
    private string statusText = "Please log in to continue.";

    public GoogleAuthService AuthService => _authService;

    public LoginWindowViewModel()
    {
        _authService = new GoogleAuthService();
    }

    public event EventHandler? LoginSucceeded;

    [RelayCommand]
    private async Task LoginAsync()
    {
        try
        {
            StatusText = "Authenticating...";
            
            // Log all potential paths
            var baseDir = AppContext.BaseDirectory;
            var paths = new[]
            {
                Path.Combine(baseDir, "Resources", "credentials.json"),
                Path.Combine(baseDir, "..", "Resources", "Resources", "credentials.json")
            };

            foreach (var path in paths)
            {
                StatusText = $"Checking path: {path}\nExists: {File.Exists(path)}";
                Log.Information($"Checking credentials at: {path} (Exists: {File.Exists(path)})");
                await Task.Delay(2000); // Give time to read the status
            }

            bool isAuthenticated = await _authService.AuthenticateAsync();

            if (isAuthenticated)
            {
                Log.Information("User authenticated successfully.");
                StatusText = "Authentication successful! Opening application...";
                
                // Small delay to show success message
                await Task.Delay(1000);
                
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Log.Warning("Authentication failed.");
                StatusText = "Authentication failed. Please try again.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Log.Error(ex, "Login failed");
        }
    }
}
