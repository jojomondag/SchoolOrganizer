using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace SchoolOrganizer.Src.Converters;

/// <summary>
/// Legacy converter for boolean to color conversion.
/// Now uses ParameterizedConverter internally for consistency.
/// Consider migrating to ParameterizedConverter with parameter "Green|Gray".
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    private static readonly ParameterizedConverter _parameterizedConverter = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Use ParameterizedConverter with the same logic
        return _parameterizedConverter.Convert(value, targetType, "Green|Gray", culture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Legacy converter for boolean to status text conversion.
/// Now uses ParameterizedConverter internally for consistency.
/// Consider migrating to ParameterizedConverter with parameter "Download completed|Not downloaded".
/// </summary>
public class BoolToStatusTextConverter : IValueConverter
{
    private static readonly ParameterizedConverter _parameterizedConverter = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Use ParameterizedConverter with the same logic
        return _parameterizedConverter.Convert(value, targetType, "Download completed|Not downloaded", culture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
