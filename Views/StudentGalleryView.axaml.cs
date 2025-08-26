using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchoolOrganizer.ViewModels;
using SchoolOrganizer.Views.ImageSelector;

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
            oldViewModel.StudentImageChangeRequested -= HandleStudentImageChangeRequested;
        }
        
        // Subscribe to new ViewModel
        if (DataContext is StudentGalleryViewModel viewModel)
        {
            System.Diagnostics.Debug.WriteLine("ViewModel found via DataContextChanged, subscribing to events");
            viewModel.AddStudentRequested += HandleAddStudentRequested;
            viewModel.StudentImageChangeRequested += HandleStudentImageChangeRequested;
            
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

    private async void HandleStudentImageChangeRequested(object? sender, SchoolOrganizer.Models.Student student)
    {
        System.Diagnostics.Debug.WriteLine($"HandleStudentImageChangeRequested called for: {student.Name}");
        await HandleStudentImageChange(student);
    }

    private async System.Threading.Tasks.Task HandleAddStudent()
    {
        try
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow != null)
            {
                var selectedImage = await ImageSelectorWindow.ShowAsync(parentWindow);
                
                if (!string.IsNullOrEmpty(selectedImage))
                {
                    // Here you would normally create a new student with the selected image
                    // For demonstration, we'll just show a simple message
                    System.Diagnostics.Debug.WriteLine($"Selected image: {selectedImage}");
                    
                    // You could show a dialog or navigate to a student creation form
                    // For now, let's just demonstrate that the image selector works
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing image selector: {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task HandleStudentImageChange(SchoolOrganizer.Models.Student student)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"HandleStudentImageChange started for: {student.Name}");
            
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow != null)
            {
                System.Diagnostics.Debug.WriteLine("Opening ImageSelectorWindow...");
                var selectedImage = await ImageSelectorWindow.ShowAsync(parentWindow);
                
                if (!string.IsNullOrEmpty(selectedImage))
                {
                    System.Diagnostics.Debug.WriteLine($"Image selected: {selectedImage}");
                    if (DataContext is StudentGalleryViewModel viewModel)
                    {
                        await viewModel.UpdateStudentImage(student, selectedImage);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No image selected");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Parent window is null");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error changing student image: {ex.Message}");
        }
    }
}
