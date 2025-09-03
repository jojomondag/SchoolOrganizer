using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SchoolOrganizer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase currentViewModel;

    [ObservableProperty]
    private string greeting = "Welcome to School Organizer!";

    public MainWindowViewModel()
    {
        CurrentViewModel = new StudentGalleryViewModel();
    }

    public bool IsStudentGalleryActive => CurrentViewModel is StudentGalleryViewModel;

    public bool IsHomeActive => CurrentViewModel is HomeViewModel;

    public IBrush ActiveContentBrush => CurrentViewModel switch
    {
        StudentGalleryViewModel => Brushes.White,
        HomeViewModel => new SolidColorBrush(Color.Parse("#f8f9fa")),
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
        CurrentViewModel = new StudentGalleryViewModel();
    }

    [RelayCommand]
    private void NavigateToHome()
    {
        CurrentViewModel = new HomeViewModel();
    }
}

// Placeholder for future home view
public class HomeViewModel : ViewModelBase
{
    public string Title { get; } = "Home";
    public string Message { get; } = "Welcome to the School Organizer application!";
}
