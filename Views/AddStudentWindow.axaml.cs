using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SchoolOrganizer.Views;

public partial class AddStudentWindow : Window
{
    public class AddStudentState : ObservableObject
    {
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
    public string StudentClass => ClassBox.Text ?? string.Empty;
    public string StudentMentor => MentorBox.Text ?? string.Empty;
    public string StudentEmail => EmailBox.Text ?? string.Empty;
    public DateTime? EnrollmentDate => EnrollmentPicker.SelectedDate?.DateTime;
    public string SelectedImagePath => state.SelectedImagePath;

    public AddStudentWindow()
    {
        InitializeComponent();
        DataContext = state;
        EnrollmentPicker.SelectedDate = DateTimeOffset.Now;
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


