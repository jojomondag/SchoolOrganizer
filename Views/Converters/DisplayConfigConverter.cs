using System;
using System.Globalization;
using Avalonia.Data.Converters;
using SchoolOrganizer.Models;

namespace SchoolOrganizer.Views.Converters;

/// <summary>
/// Converts ProfileCardDisplayConfig to specific display properties
/// This replaces multiple separate converters with a single unified approach
/// </summary>
public class DisplayConfigConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ProfileCardDisplayConfig config)
            return GetDefaultValue(parameter?.ToString());

        return parameter?.ToString() switch
        {
            "CardWidth" => config.CardWidth,
            "CardHeight" => config.CardHeight,
            "ImageSize" => config.ImageSize,
            "NameFontSize" => config.NameFontSize,
            "RoleFontSize" => config.RoleFontSize,
            "SecondaryFontSize" => config.SecondaryFontSize,
            "ShowEmail" => config.ShowEmail,
            "ShowEnrollmentDate" => config.ShowEnrollmentDate,
            "ShowSecondaryInfo" => config.ShowSecondaryInfo,
            _ => GetDefaultValue(parameter?.ToString())
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static object GetDefaultValue(string? parameter) => parameter switch
    {
        "CardWidth" => 240.0,
        "CardHeight" => 320.0,
        "ImageSize" => 90.0,
        "NameFontSize" => 16.0,
        "RoleFontSize" => 12.0,
        "SecondaryFontSize" => 10.0,
        "ShowEmail" => false,
        "ShowEnrollmentDate" => false,
        "ShowSecondaryInfo" => true,
        _ => 16.0
    };
}