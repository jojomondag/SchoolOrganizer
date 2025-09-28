using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchoolOrganizer.Models;

namespace SchoolOrganizer.Views.ProfileCard;

public partial class DetailedProfileCard : UserControl
{
    public event EventHandler<Student>? ImageClicked;
    public event EventHandler? BackToGalleryRequested;

    public DetailedProfileCard()
    {
        InitializeComponent();
        
        // Wire up the back button click event
        BackButton.Click += (sender, e) => 
        {
            System.Diagnostics.Debug.WriteLine("Back button clicked in DetailedProfileCard");
            BackToGalleryRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    private void OnProfileImagePointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        // Only respond to primary button releases
        try
        {
            System.Diagnostics.Debug.WriteLine("DetailedProfileCard image pointer released event fired");
            if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left)
            {
                if (DataContext is Student student)
                {
                    System.Diagnostics.Debug.WriteLine($"DetailedProfileCard image clicked for student: {student.Name}");
                    ImageClicked?.Invoke(this, student);
                    e.Handled = true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("DetailedProfileCard DataContext is not a Student");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DetailedProfileCard image pointer handler error: {ex.Message}");
        }
    }
}