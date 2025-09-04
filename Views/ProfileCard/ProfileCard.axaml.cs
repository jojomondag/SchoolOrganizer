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

    private void OnProfileImagePointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        // Only respond to primary button releases
        try
        {
            if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left)
            {
                if (DataContext is Student student)
                {
                    ImageClicked?.Invoke(this, student);
                    e.Handled = true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ProfileImage pointer handler error: {ex.Message}");
        }
    }
}