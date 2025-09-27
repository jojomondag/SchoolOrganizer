using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using SchoolOrganizer.ViewModels;
using SchoolOrganizer.Services;

namespace SchoolOrganizer.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        
        // Subscribe to login success event
        if (DataContext is LoginWindowViewModel viewModel)
        {
            viewModel.LoginSucceeded += OnLoginSucceeded;
        }
    }
    
    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        // Close this window and open MainWindow
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var authService = ((LoginWindowViewModel)DataContext!).AuthService;
            var mainWindow = new MainWindow();
            mainWindow.DataContext = new MainWindowViewModel(authService);
            
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            
            this.Close();
        }
    }
}
