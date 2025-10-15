using System;
using System.Globalization;
using Avalonia.Data.Converters;
using SchoolOrganizer.Src.Models.UI;

namespace SchoolOrganizer.Src.Converters;

/// <summary>
/// Converts ProfileCardDisplayLevel to boolean visibility values
/// Used to show/hide different card sizes based on current display level
/// </summary>
public class DisplayConfigConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ProfileCardDisplayLevel displayLevel)
            return false;

        return parameter?.ToString() switch
        {
            "IsSmall" => displayLevel == ProfileCardDisplayLevel.Small,
            "IsMedium" => displayLevel == ProfileCardDisplayLevel.Medium,
            "IsFull" => displayLevel == ProfileCardDisplayLevel.Full,
            _ => false
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}