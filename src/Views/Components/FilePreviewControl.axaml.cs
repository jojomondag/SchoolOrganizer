using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SchoolOrganizer.Src.Models.Assignments;
using SchoolOrganizer.Src.Services;

namespace SchoolOrganizer.Src.Views.Components;

public partial class FilePreviewControl : UserControl
{
    private readonly ScrollAnimationService _scrollAnimationService;
    private readonly FileHandlingService _fileHandlingService;
    private readonly PanelManagementService _panelManagementService;
    private bool _isContentMaximized = false;

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
        _scrollAnimationService = new ScrollAnimationService();
        _fileHandlingService = new FileHandlingService();
        _panelManagementService = new PanelManagementService();
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
        Serilog.Log.Information("FilePreviewControl.OnMaximizerClick called");
        
        if (sender is Button button)
        {
            Serilog.Log.Information("Button found, looking for ContentScrollViewer");
            var contentScrollViewer = this.FindControl<ScrollViewer>("ContentScrollViewer");
            var maximizerIcon = button.Content as Material.Icons.Avalonia.MaterialIcon;
            
            Serilog.Log.Information("ContentScrollViewer found: {HasScrollViewer}, MaximizerIcon found: {HasIcon}", 
                contentScrollViewer != null, maximizerIcon != null);
            
            if (contentScrollViewer != null && maximizerIcon != null)
            {
                _isContentMaximized = !_isContentMaximized;
                Serilog.Log.Information("Toggling content maximization. New state: {IsMaximized}", _isContentMaximized);
                
                _panelManagementService.ToggleContentMaximization(button, contentScrollViewer, maximizerIcon, !_isContentMaximized);
                
                // Raise the routed event to notify parent
                Serilog.Log.Information("Raising MaximizeClickedEvent routed event");
                RaiseEvent(new MaximizeClickedEventArgs(this));
                Serilog.Log.Information("MaximizeClickedEvent raised successfully");
            }
            else
            {
                Serilog.Log.Warning("Cannot maximize: ContentScrollViewer or MaximizerIcon is null");
            }
        }
        else
        {
            Serilog.Log.Warning("OnMaximizerClick called with non-Button sender: {SenderType}", sender?.GetType().Name);
        }
    }

    private void OnMaximizerButtonPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (sender is Button button && button.Content is Material.Icons.Avalonia.MaterialIcon icon)
        {
            icon.Kind = _isContentMaximized ? Material.Icons.MaterialIconKind.FullscreenExit : Material.Icons.MaterialIconKind.Fullscreen;
        }
    }

    private void OnMaximizerButtonPointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (sender is Button button && button.Content is Material.Icons.Avalonia.MaterialIcon icon)
        {
            icon.Kind = _isContentMaximized ? Material.Icons.MaterialIconKind.FullscreenExit : Material.Icons.MaterialIconKind.Fullscreen;
        }
    }
}
