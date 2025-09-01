using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SchoolOrganizer.Views;

public class StringIsNullOrWhiteSpaceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;
        return string.IsNullOrWhiteSpace(text);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value;
}


