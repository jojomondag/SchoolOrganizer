using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SchoolOrganizer.Src.Converters;

/// <summary>
/// Simple math converter for basic arithmetic operations.
/// Parameter format: "operation,value" (e.g., "Subtract,6", "Add,10")
/// </summary>
public class SimpleMathConverter : IValueConverter
{
    public static readonly SimpleMathConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double doubleValue && parameter is string param)
        {
            var parts = param.Split(',');
            if (parts.Length == 2 && double.TryParse(parts[1], out double operand))
            {
                return parts[0].ToLowerInvariant() switch
                {
                    "subtract" => doubleValue - operand,
                    "add" => doubleValue + operand,
                    "multiply" => doubleValue * operand,
                    "divide" => doubleValue / operand,
                    _ => doubleValue
                };
            }
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
