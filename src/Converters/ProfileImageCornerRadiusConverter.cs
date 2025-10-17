using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace SchoolOrganizer.Src.Converters;

/// <summary>
/// Converts a double value to a CornerRadius by dividing by 1.2 for a larger border radius
/// </summary>
public class ProfileImageCornerRadiusConverter : IValueConverter
{
    public static readonly ProfileImageCornerRadiusConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            var radius = doubleValue / 2.0; // Correct formula for circular border
            return new CornerRadius(radius);
        }
        return new CornerRadius(0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
