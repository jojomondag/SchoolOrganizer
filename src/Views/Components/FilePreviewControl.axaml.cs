using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SchoolOrganizer.Src.Models.Assignments;
using SchoolOrganizer.Src.Services;
using Serilog;

namespace SchoolOrganizer.Src.Views.Components;

public partial class FilePreviewControl : UserControl
{
    private readonly FileHandlingService _fileHandlingService;
    private readonly PanelManagementService _panelManagementService;

    // Routed event to notify parent when maximize button is clicked
    public static readonly RoutedEvent MaximizeClickedEvent = 
        RoutedEvent.Register<FilePreviewControl, RoutedEventArgs>("MaximizeClicked", RoutingStrategies.Bubble);

    // Custom event args to pass the FilePreviewControl instance
    public class MaximizeClickedEventArgs : RoutedEventArgs
    {
        public FilePreviewControl FilePreviewControl { get; }

        public MaximizeClickedEventArgs(FilePreviewControl filePreviewControl) : base(MaximizeClickedEvent)
        {
            FilePreviewControl = filePreviewControl;
        }
    }

    public FilePreviewControl()
    {
        InitializeComponent();
        _fileHandlingService = new FileHandlingService();
        _panelManagementService = new PanelManagementService();
        
        // Add debugging for DataContext changes
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is StudentFile file)
        {
            Log.Information("FilePreviewControl DataContext changed - File: {FileName}, IsGoogleDoc: {IsGoogleDoc}, IsImage: {IsImage}, IsCode: {IsCode}, IsText: {IsText}, IsBinary: {IsBinary}", 
                file.FileName, file.IsGoogleDoc, file.IsImage, file.IsCode, file.IsText, file.IsBinary);
        }
        else
        {
            Log.Warning("FilePreviewControl DataContext is not a StudentFile. Type: {Type}",
                DataContext?.GetType().Name ?? "null");
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnFileOpenClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is StudentFile studentFile)
        {
            _fileHandlingService.OpenFile(studentFile);
        }
    }

    private void OnOpenFolderButtonPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (sender is Button button && button.Content is Material.Icons.Avalonia.MaterialIcon icon)
        {
            icon.Kind = Material.Icons.MaterialIconKind.FolderOpen;
        }
    }

    private void OnOpenFolderButtonPointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (sender is Button button && button.Content is Material.Icons.Avalonia.MaterialIcon icon)
        {
            icon.Kind = Material.Icons.MaterialIconKind.Folder;
        }
    }

    private void OnMaximizerClick(object? sender, RoutedEventArgs e)
    {
        // Toggle IsExpanded for code/text files to show/hide full content
        if (DataContext is StudentFile file && (file.IsCode || file.IsText))
        {
            file.IsExpanded = !file.IsExpanded;
            Log.Information("FilePreviewControl - Toggled file expansion: {FileName}, IsExpanded: {IsExpanded}", 
                file.FileName, file.IsExpanded);
        }
    }
}
