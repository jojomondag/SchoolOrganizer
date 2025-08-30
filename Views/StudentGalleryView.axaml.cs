using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchoolOrganizer.ViewModels;


namespace SchoolOrganizer.Views;

public partial class StudentGalleryView : UserControl
{
    public StudentGalleryViewModel? ViewModel => DataContext as StudentGalleryViewModel;

    public StudentGalleryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("StudentGalleryView DataContextChanged called");
        
        // Unsubscribe from previous ViewModel if any
        if (sender is StudentGalleryView view && view.Tag is StudentGalleryViewModel oldViewModel)
        {
            System.Diagnostics.Debug.WriteLine("Unsubscribing from old ViewModel");
            oldViewModel.AddStudentRequested -= HandleAddStudentRequested;
        }
        
        // Subscribe to new ViewModel
        if (DataContext is StudentGalleryViewModel viewModel)
        {
            System.Diagnostics.Debug.WriteLine("ViewModel found via DataContextChanged, subscribing to events");
            viewModel.AddStudentRequested += HandleAddStudentRequested;
            
            // Store reference for cleanup
            Tag = viewModel;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"DataContext is not StudentGalleryViewModel, it's: {DataContext?.GetType().Name ?? "null"}");
        }
    }

    private async void HandleAddStudentRequested(object? sender, EventArgs e)
    {
        await HandleAddStudent();
    }

    private async System.Threading.Tasks.Task HandleAddStudent()
    {
        try
        {
            // TODO: Implement student creation logic
            System.Diagnostics.Debug.WriteLine("Add student requested");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding student: {ex.Message}");
        }
    }
}
