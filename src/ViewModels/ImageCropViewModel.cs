using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolOrganizer.Src.Models.Students;

namespace SchoolOrganizer.Src.ViewModels;

public partial class ImageCropViewModel : ObservableObject
{
    [ObservableProperty]
    private int? studentId;

    [ObservableProperty]
    private string? originalImagePath;

    [ObservableProperty]
    private string? cropSettings;

    // Events
    public event EventHandler? CancelRequested;
    public event EventHandler<(string imagePath, string? cropSettings, string? originalImagePath)>? ImageSaved;

    public ImageCropViewModel()
    {
    }

    /// <summary>
    /// Initializes the view model for editing a student's image
    /// </summary>
    public void InitializeForStudent(int studentId, string? existingOriginalImagePath = null, string? existingCropSettings = null)
    {
        StudentId = studentId;
        OriginalImagePath = existingOriginalImagePath;
        CropSettings = existingCropSettings;
    }

    /// <summary>
    /// Requests cancellation and navigation back
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Notifies that an image was saved successfully
    /// </summary>
    public void NotifyImageSaved(string imagePath, string? cropSettings, string? originalImagePath)
    {
        ImageSaved?.Invoke(this, (imagePath, cropSettings, originalImagePath));
    }
}
