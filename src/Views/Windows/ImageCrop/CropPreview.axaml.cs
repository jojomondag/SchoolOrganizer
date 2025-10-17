using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using System;
using System.IO;
using SchoolOrganizer.Src.Views.ProfileCards.Components;
using SchoolOrganizer.Src.Converters;
namespace SchoolOrganizer.Src.Views.Windows.ImageCrop;
public partial class CropPreview : UserControl
{
    public event EventHandler? BackClicked;
    public event EventHandler? ResetClicked;
    public event EventHandler? SaveClicked;
    
    private string? _tempFilePath;
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
            // Save the cropped bitmap to a reusable temporary file for the ProfileImage to display
            var tempPath = SaveBitmapToReusableTempFile(preview);
            profileImageBorder.ImagePath = tempPath;
        }
        else
        {
            profileImageBorder.ImagePath = "";
        }
    }

    public void UpdatePreviewFromPath(string imagePath)
    {
        var profileImageBorder = this.FindControl<ProfileImage>("PreviewImageBorder");
        if (profileImageBorder == null) return;
        
        System.Diagnostics.Debug.WriteLine($"CropPreview.UpdatePreviewFromPath called with: {imagePath}");
        System.Diagnostics.Debug.WriteLine($"File exists: {File.Exists(imagePath)}");
        
        // Force a complete refresh by clearing and resetting the ImagePath
        profileImageBorder.ImagePath = "";
        
        // Use Dispatcher to ensure the UI updates on the UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Clear the cache for this path to ensure fresh image loading
            UniversalImageConverter.ClearCache(imagePath);
            
            profileImageBorder.ImagePath = imagePath;
            System.Diagnostics.Debug.WriteLine($"CropPreview ProfileImage.ImagePath set to: {profileImageBorder.ImagePath}");
            
            // Property change notification is automatic when setting ImagePath
        }, Avalonia.Threading.DispatcherPriority.Render);
    }

    private string SaveBitmapToReusableTempFile(Bitmap bitmap)
    {
        try
        {
            // Create temp file path only once
            if (string.IsNullOrEmpty(_tempFilePath))
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "SchoolOrganizer", "CropPreview");
                Directory.CreateDirectory(tempDir);
                _tempFilePath = Path.Combine(tempDir, "crop_preview.png");
            }
            
            // Save bitmap to the same reusable file
            bitmap.Save(_tempFilePath);
            
            return _tempFilePath;
        }
        catch (Exception)
        {
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
    
    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        // Clean up temp file when control is removed from visual tree
        if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
        {
            try
            {
                File.Delete(_tempFilePath);
            }
            catch (Exception)
            {
                // Silently handle cleanup errors
            }
        }
        base.OnDetachedFromVisualTree(e);
    }
}