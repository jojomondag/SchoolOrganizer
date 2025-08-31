using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace SchoolOrganizer.Views;

public partial class AddStudentWindow : Window
{
    public enum Mode { Add, Edit }

    public class AddStudentState : ObservableObject
    {
        public ObservableCollection<string> AvailableClasses { get; } = new();
        public ObservableCollection<string> AvailableMentors { get; } = new();
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
    public string StudentMentor => (MentorBox.SelectedItem as string) ?? string.Empty;
    public string StudentEmail => EmailBox.Text ?? string.Empty;
    public DateTime? EnrollmentDate => EnrollmentPicker.SelectedDate?.DateTime;
    public string SelectedImagePath => state.SelectedImagePath;

    public AddStudentWindow()
    {
        InitializeComponent();
        DataContext = state;
        EnrollmentPicker.SelectedDate = DateTimeOffset.Now;
    }

    public void InitializeForEdit(Models.Student student)
    {
        TitleText.Text = "Edit Student";
        PrimaryButton.Content = "Save";

        NameBox.Text = student.Name;
        ClassBox.SelectedItem = student.ClassName;
        MentorBox.SelectedItem = student.Mentor;
        EmailBox.Text = student.Email;
        EnrollmentPicker.SelectedDate = new DateTimeOffset(student.EnrollmentDate);
        state.SelectedImagePath = student.PictureUrl;
    }

    public void LoadOptionsFromStudents(System.Collections.Generic.IEnumerable<Models.Student> students)
    {
        state.AvailableClasses.Clear();
        state.AvailableMentors.Clear();

        foreach (var cls in System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(students, s => s.ClassName)))
        {
            if (!string.IsNullOrWhiteSpace(cls)) state.AvailableClasses.Add(cls);
        }
        foreach (var m in System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(students, s => s.Mentor)))
        {
            if (!string.IsNullOrWhiteSpace(m)) state.AvailableMentors.Add(m);
        }
    }

    private async void OnChooseImageClick(object? sender, RoutedEventArgs e)
    {
        var parentWindow = this;
        var path = await ImageCropWindow.ShowAsync(parentWindow);
        if (!string.IsNullOrWhiteSpace(path))
        {
            state.SelectedImagePath = path;
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
            Mentor = StudentMentor,
            Email = StudentEmail,
            EnrollmentDate = EnrollmentDate ?? DateTime.Now,
            PicturePath = SelectedImagePath
        });
    }

    public class AddedStudentResult
    {
        public string Name { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string Mentor { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime EnrollmentDate { get; set; }
        public string PicturePath { get; set; } = string.Empty;
    }
}


