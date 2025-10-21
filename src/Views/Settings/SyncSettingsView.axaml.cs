using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SchoolOrganizer.Src.Views.Settings;

public partial class SyncSettingsView : UserControl
{
    public SyncSettingsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
