using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using Avalonia;
using SchoolOrganizer.ViewModels;
using System.Linq;

namespace SchoolOrganizer.Views.ClassroomDownload;

public partial class ClassroomDownloadView : UserControl
{
    public ClassroomDownloadView()
    {
        InitializeComponent();
    }

    private async void OnBrowseFolderClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Download Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var selectedPath = folders[0].Path.LocalPath;
            if (DataContext is ClassroomDownloadViewModel viewModel)
            {
                viewModel.SelectFolder(selectedPath);
            }
        }
    }

    private void OnCourseCardPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (sender is Border courseCardBorder)
        {
            Serilog.Log.Information("Course card hover entered - Background: {Background}, BorderBrush: {BorderBrush}", 
                courseCardBorder.Background, courseCardBorder.BorderBrush);
            
            if (this.FindResource("ShadowStrong") is BoxShadows hoverShadow)
                courseCardBorder.BoxShadow = hoverShadow;
        }
    }

    private void OnCourseCardPointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (sender is Border courseCardBorder)
        {
            Serilog.Log.Information("Course card hover exited - Background: {Background}, BorderBrush: {BorderBrush}", 
                courseCardBorder.Background, courseCardBorder.BorderBrush);
            
            if (this.FindResource("ShadowLight") is BoxShadows normalShadow)
                courseCardBorder.BoxShadow = normalShadow;
        }
    }

    private void OnBrowseButtonPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (BrowseIcon != null)
        {
            BrowseIcon.Kind = Material.Icons.MaterialIconKind.FolderOpen;
        }
    }

    private void OnBrowseButtonPointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (BrowseIcon != null)
        {
            BrowseIcon.Kind = Material.Icons.MaterialIconKind.Folder;
        }
    }
}
