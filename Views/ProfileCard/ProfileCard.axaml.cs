using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchoolOrganizer.Models;

namespace SchoolOrganizer.Views.ProfileCard;

public partial class ProfileCard : UserControl
{
    public event EventHandler<Student>? ImageClicked;

    public ProfileCard()
    {
        InitializeComponent();
    }

    private void OnProfileImageClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is Student student)
        {
            ImageClicked?.Invoke(this, student);
        }
    }
}