using System;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System.Globalization;
using System.IO;

namespace SchoolOrganizer.Views;

public class UniversalImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            try
            {
                if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    return new Bitmap(path);
                }

                if (File.Exists(path))
                {
                    // Create a new Bitmap instance to ensure the UI updates
                    // This helps when the same file path is used but the content has changed
                    return new Bitmap(path);
                }
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value;
}


