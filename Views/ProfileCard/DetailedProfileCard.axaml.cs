using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchoolOrganizer.Models;

namespace SchoolOrganizer.Views.ProfileCard;

public partial class DetailedProfileCard : UserControl
{
    public event EventHandler<Student>? ImageClicked;

    public DetailedProfileCard()
    {
        InitializeComponent();
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
            System.Diagnostics.Debug.WriteLine($"DetailedProfileCard image pointer handler error: {ex.Message}");
        }
    }
}