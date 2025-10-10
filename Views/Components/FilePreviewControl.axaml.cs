using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SchoolOrganizer.Models;
using SchoolOrganizer.Services;

namespace SchoolOrganizer.Views.Components;

public partial class FilePreviewControl : UserControl
{
    private readonly ScrollAnimationService _scrollAnimationService;
    private readonly FileHandlingService _fileHandlingService;
    private readonly PanelManagementService _panelManagementService;
    private bool _isContentMaximized = false;

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
        if (sender is Button button)
        {
            var contentScrollViewer = this.FindControl<ScrollViewer>("ContentScrollViewer");
            var maximizerIcon = button.Content as Material.Icons.Avalonia.MaterialIcon;
            
            if (contentScrollViewer != null && maximizerIcon != null)
            {
                _isContentMaximized = !_isContentMaximized;
                _panelManagementService.ToggleContentMaximization(button, contentScrollViewer, maximizerIcon, !_isContentMaximized);
            }
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
