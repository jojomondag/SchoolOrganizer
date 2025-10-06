using System;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace SchoolOrganizer.Services;

public enum AppTheme { Light, Dark }

public static class ThemeManager
{
    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;
    public static event EventHandler<AppTheme>? ThemeChanged;

    public static void ApplyTheme(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null) return;
        CurrentTheme = theme;
        app.RequestedThemeVariant = theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var merged = app.Resources.MergedDictionaries;
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i] is ResourceInclude ri)
            {
                var s = ri.Source?.ToString();
                if (s?.EndsWith("LightTheme.axaml", StringComparison.OrdinalIgnoreCase) == true ||
                    s?.EndsWith("DarkTheme.axaml", StringComparison.OrdinalIgnoreCase) == true)
                {
                    merged.RemoveAt(i);
                }
            }
        }
        var name = theme == AppTheme.Dark ? "Dark" : "Light";
        var uri = new Uri($"avares://SchoolOrganizer/Views/Styles/{name}Theme.axaml");
        merged.Add(new ResourceInclude(uri) { Source = uri });
        ThemeChanged?.Invoke(null, theme);
    }
    public static void ToggleTheme() => ApplyTheme(CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
    public static void Initialize() => ApplyTheme(CurrentTheme);
}
