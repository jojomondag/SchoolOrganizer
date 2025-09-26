using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using Avalonia.Input;

namespace SchoolOrganizer.Views;

public partial class AddStudentWindow : Window
{
    public enum Mode { Add, Edit }

    public class AddStudentState : ObservableObject
    {
        public ObservableCollection<string> AvailableClasses { get; } = new();
        public ObservableCollection<string> AvailableMentors { get; } = new();
        public ObservableCollection<string> SelectedMentors { get; } = new();
        
        private string selectedImagePath = string.Empty;
        public string SelectedImagePath
        {
            get => selectedImagePath;
            set
            {
                if (SetProperty(ref selectedImagePath, value))
                {
                    OnPropertyChanged(nameof(IsImageMissing));
                }
            }
        }

        public bool IsImageMissing => string.IsNullOrWhiteSpace(SelectedImagePath);
    }

    private readonly AddStudentState state = new();

    public string StudentName => NameBox.Text ?? string.Empty;
    public string StudentClass => (ClassBox.SelectedItem as string) ?? string.Empty;
    public System.Collections.Generic.List<string> StudentMentors => new(state.SelectedMentors);
    public string StudentEmail => EmailBox.Text ?? string.Empty;
    public DateTime? EnrollmentDate => EnrollmentPicker.SelectedDate?.DateTime;
    public string SelectedImagePath => state.SelectedImagePath;

    public AddStudentWindow()
    {
        InitializeComponent();
        DataContext = state;
        EnrollmentPicker.SelectedDate = DateTimeOffset.Now;
        
        // Add Escape key handling to close the window
        this.KeyDown += OnKeyDown;
    }

    public void InitializeForEdit(Models.Student student)
    {
        TitleText.Text = "Edit Student";
        PrimaryButton.Content = "Save";

        NameBox.Text = student.Name;
        ClassBox.SelectedItem = student.ClassName;
        
        // Load multiple mentors
        state.SelectedMentors.Clear();
        foreach (var mentor in student.Mentors)
        {
            state.SelectedMentors.Add(mentor);
        }
        
        EmailBox.Text = student.Email;
        EnrollmentPicker.SelectedDate = new DateTimeOffset(student.EnrollmentDate);
        state.SelectedImagePath = student.PictureUrl;
        // Store the context student to enable preloading original image when clicking avatar
        Tag = student;
    }

    public void LoadOptionsFromStudents(System.Collections.Generic.IEnumerable<Models.Student> students)
    {
        state.AvailableClasses.Clear();
        state.AvailableMentors.Clear();

        foreach (var cls in System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(students, s => s.ClassName)))
        {
            if (!string.IsNullOrWhiteSpace(cls)) state.AvailableClasses.Add(cls);
        }
        
        // Extract all mentors from all students
        var allMentors = students.SelectMany(s => s.Mentors).Distinct();
        foreach (var mentor in allMentors)
        {
            if (!string.IsNullOrWhiteSpace(mentor)) state.AvailableMentors.Add(mentor);
        }
    }

    private async void OnChooseImageClick(object? sender, RoutedEventArgs e)
    {
        var parentWindow = this;
        string? path;
        if (string.Equals(TitleText.Text, "Edit Student", StringComparison.OrdinalIgnoreCase) && Tag is SchoolOrganizer.Models.Student ctx)
        {
            path = await ImageCropWindow.ShowForStudentAsync(parentWindow, ctx.Id);
        }
        else
        {
            path = await ImageCropWindow.ShowAsync(parentWindow);
        }
        if (!string.IsNullOrWhiteSpace(path))
        {
            state.SelectedImagePath = path;
            // Force a refresh in case the file isn't immediately readable
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await System.Threading.Tasks.Task.Delay(150);
                var current = state.SelectedImagePath;
                if (string.Equals(current, path, StringComparison.Ordinal))
                {
                    state.SelectedImagePath = string.Empty;
                    await System.Threading.Tasks.Task.Delay(1);
                    state.SelectedImagePath = path;
                }
            });
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(StudentName))
        {
            ValidationText.Text = "Name is required";
            return;
        }

        Close(new AddedStudentResult
        {
            Name = StudentName,
            ClassName = StudentClass,
            Mentors = StudentMentors,
            Email = StudentEmail,
            EnrollmentDate = EnrollmentDate ?? DateTime.Now,
            PicturePath = SelectedImagePath
        });
    }

    private void OnAddMentorClick(object? sender, RoutedEventArgs e)
    {
        MentorBox.IsVisible = true;
        MentorBox.IsDropDownOpen = true;
    }

    private void OnMentorSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (MentorBox.SelectedItem is string selectedMentor && 
            !string.IsNullOrWhiteSpace(selectedMentor) &&
            !state.SelectedMentors.Contains(selectedMentor))
        {
            state.SelectedMentors.Add(selectedMentor);
        }
        
        MentorBox.SelectedItem = null;
        MentorBox.IsVisible = false;
    }

    private void OnRemoveMentorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string mentorToRemove)
        {
            state.SelectedMentors.Remove(mentorToRemove);
        }
    }

    public class AddedStudentResult
    {
        public string Name { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> Mentors { get; set; } = new();
        public string Email { get; set; } = string.Empty;
        public DateTime EnrollmentDate { get; set; }
        public string PicturePath { get; set; } = string.Empty;
    }
}


