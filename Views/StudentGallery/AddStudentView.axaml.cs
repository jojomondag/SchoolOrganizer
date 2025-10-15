using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using SchoolOrganizer.ViewModels;
using SchoolOrganizer.Views.Windows.ImageCrop;

namespace SchoolOrganizer.Views.StudentGallery;

public partial class AddStudentView : UserControl
{
    public AddStudentViewModel? ViewModel => DataContext as AddStudentViewModel;

    public AddStudentView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        
        // Add Escape key handling to close the view
        this.KeyDown += OnKeyDown;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Subscribe to ViewModel events
        if (ViewModel != null)
        {
            ViewModel.StudentAdded += OnStudentAdded;
            ViewModel.MultipleStudentsAdded += OnMultipleStudentsAdded;
            ViewModel.Cancelled += OnCancelled;
        }
    }

    private void OnStudentAdded(object? sender, AddStudentViewModel.AddedStudentResult result)
    {
        // This will be handled by the parent StudentGalleryViewModel
        // The event is just for the ViewModel to communicate completion
    }

    private void OnMultipleStudentsAdded(object? sender, System.Collections.Generic.List<AddStudentViewModel.AddedStudentResult> results)
    {
        // This will be handled by the parent StudentGalleryViewModel
        // The event is just for the ViewModel to communicate completion
    }

    private void OnCancelled(object? sender, EventArgs e)
    {
        // This will be handled by the parent StudentGalleryViewModel
        // The event is just for the ViewModel to communicate cancellation
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ViewModel?.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async void OnChooseImageClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        if (parentWindow == null) return;

        string? path = await ImageCropWindow.ShowAsync(parentWindow);
        if (!string.IsNullOrWhiteSpace(path))
        {
            ViewModel.SelectedImagePath = path;
            // Force a refresh in case the file isn't immediately readable
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await System.Threading.Tasks.Task.Delay(150);
                var current = ViewModel.SelectedImagePath;
                if (string.Equals(current, path, StringComparison.Ordinal))
                {
                    ViewModel.SelectedImagePath = string.Empty;
                    await System.Threading.Tasks.Task.Delay(1);
                    ViewModel.SelectedImagePath = path;
                }
            });
        }
    }

    private void OnAddTeacherClick(object? sender, RoutedEventArgs e)
    {
        var teacherBox = this.FindControl<ComboBox>("TeacherBox");
        if (teacherBox != null)
        {
            teacherBox.IsVisible = true;
            teacherBox.IsDropDownOpen = true;
        }
    }

    private void OnTeacherSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null) return;

        var teacherBox = this.FindControl<ComboBox>("TeacherBox");
        if (teacherBox?.SelectedItem is string selectedTeacher && 
            !string.IsNullOrWhiteSpace(selectedTeacher) &&
            !ViewModel.SelectedTeachers.Contains(selectedTeacher))
        {
            ViewModel.SelectedTeachers.Add(selectedTeacher);
        }
        
        if (teacherBox != null)
        {
            teacherBox.SelectedItem = null;
            teacherBox.IsVisible = false;
        }
    }

    private void OnRemoveTeacherClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string teacherToRemove && ViewModel != null)
        {
            ViewModel.RemoveTeacherCommand.Execute(teacherToRemove);
        }
    }

    private void OnClassroomSelected(object? sender, SelectionChangedEventArgs e)
    {
        // The SelectionChanged event will trigger the property change
        // which will automatically call the OnClassroomSelected method in the ViewModel
        // No need to manually call the command
    }
}
