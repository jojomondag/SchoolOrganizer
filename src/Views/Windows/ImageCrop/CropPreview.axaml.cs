using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using System;
using System.IO;
namespace SchoolOrganizer.Src.Views.Windows.ImageCrop;
public partial class CropPreview : UserControl
{
    public event EventHandler? BackClicked;
    public event EventHandler? RotateLeftClicked;
    public event EventHandler? RotateRightClicked;
    public event EventHandler? ResetClicked;
    public event EventHandler? SaveClicked;
    
    public CropPreview()
    {
        InitializeComponent();
    }
    public void UpdatePreview(Bitmap? preview)
    {
        var previewImage = this.FindControl<Image>("PreviewImage");
        if (previewImage == null) return;
        
        if (preview != null)
        {
            // Direct bitmap assignment for final preview - no file I/O needed
            previewImage.Source = preview;
        }
        else
        {
            previewImage.Source = null;
        }
    }

    public void UpdatePreviewDirect(Bitmap? preview)
    {
        var previewImage = this.FindControl<Image>("PreviewImage");
        if (previewImage == null) return;
        
        if (preview != null)
        {
            // Direct bitmap assignment for live updates - much faster than file I/O
            previewImage.Source = preview;
        }
        else
        {
            previewImage.Source = null;
        }
    }

    public void UpdatePreviewFromPath(string imagePath)
    {
        var previewImage = this.FindControl<Image>("PreviewImage");
        if (previewImage == null) return;
        
        System.Diagnostics.Debug.WriteLine($"CropPreview.UpdatePreviewFromPath called with: {imagePath}");
        System.Diagnostics.Debug.WriteLine($"File exists: {File.Exists(imagePath)}");
        
        if (File.Exists(imagePath))
        {
            // Load image directly from file path
            previewImage.Source = new Bitmap(imagePath);
        }
        else
        {
            previewImage.Source = null;
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
    public void SetRotateLeftEnabled(bool enabled)
    {
        var btn = this.FindControl<Button>("RotateLeftButton");
        if (btn != null) btn.IsEnabled = enabled;
    }
    public void SetRotateRightEnabled(bool enabled)
    {
        var btn = this.FindControl<Button>("RotateRightButton");
        if (btn != null) btn.IsEnabled = enabled;
    }
    private void BackButton_Click(object? sender, RoutedEventArgs e) => BackClicked?.Invoke(this, EventArgs.Empty);
    private void RotateLeftButton_Click(object? sender, RoutedEventArgs e) => RotateLeftClicked?.Invoke(this, EventArgs.Empty);
    private void RotateRightButton_Click(object? sender, RoutedEventArgs e) => RotateRightClicked?.Invoke(this, EventArgs.Empty);
    private void ResetButton_Click(object? sender, RoutedEventArgs e) => ResetClicked?.Invoke(this, EventArgs.Empty);
    private void SaveButton_Click(object? sender, RoutedEventArgs e) => SaveClicked?.Invoke(this, EventArgs.Empty);
    
}