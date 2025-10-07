using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
}
