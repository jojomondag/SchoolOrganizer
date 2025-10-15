using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
        // Check if we're in edit mode by looking at the parent ViewModel
        var parentViewModel = GetParentStudentGalleryViewModel();
        bool isEditMode = parentViewModel?.StudentBeingEdited != null;
        
        if (isEditMode && parentViewModel != null && parentViewModel.StudentBeingEdited != null)
        {
            System.Diagnostics.Debug.WriteLine($"AddStudentView: Handling edit mode for student: {parentViewModel.StudentBeingEdited.Name}");
            
            // We're in edit mode - update the existing student
            var originalStudent = parentViewModel.StudentBeingEdited;
            
            // Update the student properties
            originalStudent.Name = result.Name;
            originalStudent.ClassName = result.ClassName;
            originalStudent.Email = result.Email;
            originalStudent.EnrollmentDate = result.EnrollmentDate.DateTime;
            originalStudent.PictureUrl = result.PicturePath ?? string.Empty;
            
            // Update teachers
            originalStudent.ClearTeachers();
            foreach (var teacher in result.Teachers ?? new System.Collections.Generic.List<string>())
                originalStudent.AddTeacher(teacher);
            
            // Publish the student updated event
            Services.StudentCoordinatorService.Instance.PublishStudentUpdated(originalStudent);
            
            // Clear the student being edited
            parentViewModel.StudentBeingEdited = null;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("AddStudentView: Handling add mode");
            
            // Convert AddStudentViewModel.AddedStudentResult to Student and publish through coordinator
            var student = new SchoolOrganizer.Models.Student
            {
                Name = result.Name,
                ClassName = result.ClassName,
                Email = result.Email,
                EnrollmentDate = result.EnrollmentDate.DateTime,
                PictureUrl = result.PicturePath ?? string.Empty
            };
            
            // Add teachers
            foreach (var teacher in result.Teachers ?? new System.Collections.Generic.List<string>())
                student.AddTeacher(teacher);
            
            // Publish the student added event
            Services.StudentCoordinatorService.Instance.PublishStudentAdded(student);
        }
        
        // Also publish completion
        Services.StudentCoordinatorService.Instance.PublishAddStudentCompleted();
    }

    private void OnMultipleStudentsAdded(object? sender, System.Collections.Generic.List<AddStudentViewModel.AddedStudentResult> results)
    {
        // Convert each result to Student and publish through coordinator
        foreach (var result in results)
        {
            var student = new SchoolOrganizer.Models.Student
            {
                Name = result.Name,
                ClassName = result.ClassName,
                Email = result.Email,
                EnrollmentDate = result.EnrollmentDate.DateTime,
                PictureUrl = result.PicturePath ?? string.Empty
            };
            
            // Add teachers
            foreach (var teacher in result.Teachers ?? new System.Collections.Generic.List<string>())
                student.AddTeacher(teacher);
            
            // Publish the student added event
            Services.StudentCoordinatorService.Instance.PublishStudentAdded(student);
        }
        
        // Also publish completion
        Services.StudentCoordinatorService.Instance.PublishAddStudentCompleted();
    }

    private void OnCancelled(object? sender, EventArgs e)
    {
        // Use StudentCoordinatorService to publish cancellation
        Services.StudentCoordinatorService.Instance.PublishAddStudentCancelled();
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

    private void OnProfileImageControlClicked(object? sender, EventArgs e)
    {
        OnChooseImageClick(sender, new RoutedEventArgs());
    }

    private StudentGalleryViewModel? GetParentStudentGalleryViewModel()
    {
        // Find the parent StudentGalleryView and get its ViewModel
        var parent = this.GetVisualParent();
        while (parent != null)
        {
            if (parent is StudentGalleryView galleryView)
            {
                return galleryView.ViewModel;
            }
            parent = parent.GetVisualParent();
        }
        return null;
    }
}
