using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Threading;
using SchoolOrganizer.Src.ViewModels;
using SchoolOrganizer.Src.Views.ProfileCards.Components;
using SchoolOrganizer.Src.Models.Students;

namespace SchoolOrganizer.Src.Views.StudentGallery;

public partial class AddStudentView : UserControl
{
    public AddStudentViewModel? ViewModel => DataContext as AddStudentViewModel;

    public AddStudentView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        
        // Add Escape key handling to close the view
        this.KeyDown += OnKeyDown;
        
        // Subscribe to ProfileImage events when view is loaded
        this.Loaded += OnViewLoaded;
        
        // Subscribe to coordinator events for image changes
        SubscribeToCoordinatorEvents();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Subscribe to ViewModel events
        if (ViewModel != null)
        {
            ViewModel.StudentAdded += OnStudentAdded;
            ViewModel.MultipleStudentsAdded += OnMultipleStudentsAdded;
            ViewModel.Cancelled += OnCancelled;
            
            // Subscribe to property changes to update the temporary student
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Update the temporary student when relevant properties change
        if (e.PropertyName == nameof(AddStudentViewModel.SelectedImagePath) ||
            e.PropertyName == nameof(AddStudentViewModel.CropSettings) ||
            e.PropertyName == nameof(AddStudentViewModel.OriginalImagePath) ||
            e.PropertyName == nameof(AddStudentViewModel.StudentName))
        {
            SetupTemporaryStudentForProfileImage();
        }
    }

    private void SubscribeToCoordinatorEvents()
    {
        var coordinator = Services.StudentCoordinatorService.Instance;
        coordinator.StudentImageUpdated += OnCoordinatorStudentImageUpdated;
    }

    private void OnCoordinatorStudentImageUpdated(object? sender, (SchoolOrganizer.Src.Models.Students.Student student, string imagePath, string? cropSettings, string? originalImagePath) args)
    {
        // Check if this is our temporary student (ID = -1)
        if (args.student.Id == -1 && ViewModel != null)
        {
            // Update on UI thread to ensure proper rendering
            Dispatcher.UIThread.Post(() =>
            {
                // Update the ViewModel with the new image data
                ViewModel.SelectedImagePath = args.imagePath;
                ViewModel.CropSettings = args.cropSettings;
                ViewModel.OriginalImagePath = args.originalImagePath;

                // Update the temporary student for the ProfileImage
                SetupTemporaryStudentForProfileImage();
                
                // Force the ProfileImage to refresh its display
                var profileImage = this.FindControl<ProfileImage>("ProfileImage");
                if (profileImage != null)
                {
                    profileImage.ForceImageRefresh();
                }
            });
        }
    }

    private void OnViewLoaded(object? sender, RoutedEventArgs e)
    {
        // Create a temporary student object for the ProfileImage to use with the coordinator service
        SetupTemporaryStudentForProfileImage();
        
        // Subscribe to ProfileImage ImageClicked event
        var profileImage = this.FindControl<ProfileImage>("ProfileImage");
        if (profileImage != null)
        {
            profileImage.ImageClicked += OnProfileImageClicked;
        }
    }

    private void OnProfileImageClicked(object? sender, EventArgs e)
    {
        // Create a temporary student object for the ProfileImage to use with the coordinator service
        if (ViewModel != null)
        {
            var tempStudent = new SchoolOrganizer.Src.Models.Students.Student
            {
                Id = -1, // Use -1 to indicate this is a temporary student
                Name = ViewModel.StudentName,
                PictureUrl = ViewModel.SelectedImagePath,
                CropSettings = ViewModel.CropSettings,
                OriginalImagePath = ViewModel.OriginalImagePath
            };

            // Use StudentCoordinatorService to request image change
            Services.StudentCoordinatorService.Instance.PublishStudentImageChangeRequested(tempStudent);
        }
    }

    private void SetupTemporaryStudentForProfileImage()
    {
        var profileImage = this.FindControl<ProfileImage>("ProfileImage");
        if (profileImage != null && ViewModel != null)
        {
            // Don't set DataContext - let the ProfileImage use the binding to SelectedImagePath
            // The ProfileImage will get its image path from the ViewModel binding
            
            // Force image refresh to ensure the new image is displayed
            if (!string.IsNullOrEmpty(ViewModel.SelectedImagePath))
            {
                profileImage.ForceImageRefresh();
            }
        }
    }

    private void OnStudentAdded(object? sender, AddStudentViewModel.AddedStudentResult result)
    {
        // Check if we're in edit mode by looking at the parent ViewModel
        var parentViewModel = GetParentStudentGalleryViewModel();
        bool isEditMode = parentViewModel?.StudentBeingEdited != null;
        
        if (isEditMode && parentViewModel != null && parentViewModel.StudentBeingEdited != null)
        {
            // We're in edit mode - update the existing student
            var originalStudent = parentViewModel.StudentBeingEdited;
            
            // Update the student properties
            originalStudent.Name = result.Name;
            originalStudent.ClassName = result.ClassName;
            originalStudent.Email = result.Email;
            originalStudent.EnrollmentDate = result.EnrollmentDate.DateTime;
            originalStudent.PictureUrl = result.PicturePath ?? string.Empty;
            
            // Update crop settings and original image path if they were modified
            if (ViewModel != null)
            {
                originalStudent.CropSettings = ViewModel.CropSettings;
                originalStudent.OriginalImagePath = ViewModel.OriginalImagePath;
            }
            
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
            // Convert AddStudentViewModel.AddedStudentResult to Student and publish through coordinator
            var student = new SchoolOrganizer.Src.Models.Students.Student
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
            var student = new SchoolOrganizer.Src.Models.Students.Student
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

    private void OnClassroomCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel != null && sender is Border border && border.DataContext is Google.Apis.Classroom.v1.Data.Course classroom)
        {
            ViewModel.SelectedClassroom = classroom;
        }
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
