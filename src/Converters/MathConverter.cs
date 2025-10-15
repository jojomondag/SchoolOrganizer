using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SchoolOrganizer.Src.Converters;

public class MathConverter : IValueConverter
{
    public static readonly MathConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double doubleValue && parameter is string param)
        {
            var parts = param.Split(',');
            if (parts.Length == 2 && double.TryParse(parts[1], out double subtractValue))
            {
                return doubleValue - subtractValue;
            }
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
