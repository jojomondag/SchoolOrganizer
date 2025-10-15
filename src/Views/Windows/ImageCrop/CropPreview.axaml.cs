using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using System;
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
        var img = this.FindControl<Image>("PreviewImage");
        if (img == null) return;
        img.Source = preview;
        img.IsVisible = preview != null;
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