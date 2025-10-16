using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using System;
using System.IO;
using SchoolOrganizer.Src.Views.ProfileCards.Components;
namespace SchoolOrganizer.Src.Views.Windows.ImageCrop;
public partial class CropPreview : UserControl
{
    public event EventHandler? BackClicked;
    public event EventHandler? ResetClicked;
    public event EventHandler? SaveClicked;
    public CropPreview()
    {
        InitializeComponent();
    }
    public void UpdatePreview(Bitmap? preview)
    {
        var profileImageBorder = this.FindControl<ProfileImage>("PreviewImageBorder");
        if (profileImageBorder == null) return;
        
        if (preview != null)
        {
            // Save the cropped bitmap to a temporary file for the ProfileImage to display
            var tempPath = SaveBitmapToTempFile(preview);
            profileImageBorder.ImagePath = tempPath;
        }
        else
        {
            profileImageBorder.ImagePath = "";
        }
    }

    private string SaveBitmapToTempFile(Bitmap bitmap)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "SchoolOrganizer", "CropPreview");
            Directory.CreateDirectory(tempDir);
            
            var tempFileName = $"crop_preview_{Guid.NewGuid():N}.png";
            var tempPath = Path.Combine(tempDir, tempFileName);
            
            // Save bitmap to file
            bitmap.Save(tempPath);
            
            return tempPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving crop preview: {ex.Message}");
            return "";
        }
    }
    public void ShowActions(bool show)
    {
        var panel = this.FindControl<StackPanel>("ActionButtonsPanel");
        if (panel != null) panel.IsVisible = show;
    }
    public void SetResetEnabled(bool enabled)
    {
        var btn = this.FindControl<Button>("ResetButton");
        if (btn != null) btn.IsEnabled = enabled;
    }
    public void SetSaveEnabled(bool enabled)
    {
        var btn = this.FindControl<Button>("SaveButton");
        if (btn != null) btn.IsEnabled = enabled;
    }
    private void BackButton_Click(object? sender, RoutedEventArgs e) => BackClicked?.Invoke(this, EventArgs.Empty);
    private void ResetButton_Click(object? sender, RoutedEventArgs e) => ResetClicked?.Invoke(this, EventArgs.Empty);
    private void SaveButton_Click(object? sender, RoutedEventArgs e) => SaveClicked?.Invoke(this, EventArgs.Empty);
}