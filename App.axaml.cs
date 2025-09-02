using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ImageSelector;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "Image Selector - Profile Picture Cropper",
                MinWidth = 800,
                MinHeight = 600,
                Width = 900,
                Height = 700,
                Content = new ImageSelectorView()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}