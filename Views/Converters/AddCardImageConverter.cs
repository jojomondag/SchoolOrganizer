using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SchoolOrganizer.Views.Converters;

/// <summary>
/// Converter that detects if this is an add card and returns appropriate visibility
/// </summary>
public class AddCardImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string pictureUrl)
        {
            return pictureUrl == "ADD_CARD";
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter that detects if this is NOT an add card and returns appropriate visibility
/// </summary>
public class RegularImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string pictureUrl)
        {
            return pictureUrl != "ADD_CARD";
        }
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter that returns 0.5 opacity for add cards, 1.0 for regular cards
/// </summary>
public class AddCardOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string pictureUrl && pictureUrl == "ADD_CARD")
        {
            return 0.5;
        }
        return 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
