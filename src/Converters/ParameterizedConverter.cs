using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Material.Icons;

namespace SchoolOrganizer.Src.Converters;

/// <summary>
/// Universal converter that handles boolean to value mappings and simple comparisons.
/// Supports colors, text, icons, and numeric values through parameter configuration.
/// 
/// Parameter formats:
/// - Boolean mapping: "TrueValue|FalseValue" (e.g., "Green|Gray", "Download completed|Not downloaded")
/// - Color values: Use hex (#00FF00) or color names (Green, Red, etc.)
/// - Material Icons: Use enum names (MenuDown, MenuUp, etc.)
/// - Text: Any string value
/// </summary>
public class ParameterizedConverter : IValueConverter
{
    public static readonly ParameterizedConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string param || string.IsNullOrWhiteSpace(param))
            return value;

        // Handle boolean to value mapping
        if (value is bool boolValue)
        {
            var parts = param.Split('|');
            if (parts.Length == 2)
            {
                var trueValue = parts[0].Trim();
                var falseValue = parts[1].Trim();
                
                return boolValue ? ParseValue(trueValue, targetType) : ParseValue(falseValue, targetType);
            }
        }

        // Handle comparison operations
        if (param.StartsWith("==") || param.StartsWith("!="))
        {
            var operatorPart = param.Substring(0, 2);
            var valuePart = param.Substring(2).Trim();
            
            if (operatorPart == "==")
                return value?.ToString() == valuePart;
            else if (operatorPart == "!=")
                return value?.ToString() != valuePart;
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static object? ParseValue(string value, Type targetType)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Handle Material Icons
        if (targetType == typeof(MaterialIconKind) || targetType == typeof(MaterialIconKind?))
        {
            if (Enum.TryParse<MaterialIconKind>(value, out var iconKind))
                return iconKind;
        }

        // Handle TextTrimming struct (not an enum)
        if (targetType == typeof(Avalonia.Media.TextTrimming))
        {
            return value.ToLowerInvariant() switch
            {
                "none" => Avalonia.Media.TextTrimming.None,
                "characterellipsis" => Avalonia.Media.TextTrimming.CharacterEllipsis,
                "wordellipsis" => Avalonia.Media.TextTrimming.WordEllipsis,
                "prefixcharacterellipsis" => Avalonia.Media.TextTrimming.PrefixCharacterEllipsis,
                _ => Avalonia.Media.TextTrimming.None
            };
        }
        else if (targetType.IsGenericType && 
                 targetType.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                 targetType.GetGenericArguments()[0] == typeof(Avalonia.Media.TextTrimming))
        {
            var trimming = value.ToLowerInvariant() switch
            {
                "none" => Avalonia.Media.TextTrimming.None,
                "characterellipsis" => Avalonia.Media.TextTrimming.CharacterEllipsis,
                "wordellipsis" => Avalonia.Media.TextTrimming.WordEllipsis,
                "prefixcharacterellipsis" => Avalonia.Media.TextTrimming.PrefixCharacterEllipsis,
                _ => Avalonia.Media.TextTrimming.None
            };
            return (Avalonia.Media.TextTrimming?)trimming;
        }

        // Handle Colors and SolidColorBrush
        if (targetType == typeof(SolidColorBrush) || targetType == typeof(IBrush))
        {
            if (value.StartsWith("#"))
            {
                if (Color.TryParse(value, out var color))
                    return new SolidColorBrush(color);
            }
            else
            {
                // Try to parse as named color
                var color = ParseNamedColor(value);
                if (color.HasValue)
                    return new SolidColorBrush(color.Value);
            }
        }

        // Handle Color type
        if (targetType == typeof(Color) || targetType == typeof(Color?))
        {
            if (value.StartsWith("#"))
            {
                if (Color.TryParse(value, out var color))
                    return color;
            }
            else
            {
                var color = ParseNamedColor(value);
                if (color.HasValue)
                    return color.Value;
            }
        }

        // Handle strings
        if (targetType == typeof(string))
            return value;

        // Handle numeric types
        if (targetType == typeof(int) || targetType == typeof(int?))
        {
            if (int.TryParse(value, out var intValue))
                return intValue;
        }

        if (targetType == typeof(double) || targetType == typeof(double?))
        {
            // Handle special "Infinity" value
            if (string.Equals(value, "Infinity", StringComparison.OrdinalIgnoreCase))
            {
                return targetType == typeof(double?) ? (double?)double.PositiveInfinity : double.PositiveInfinity;
            }
            
            if (double.TryParse(value, out var doubleValue))
                return targetType == typeof(double?) ? (double?)doubleValue : doubleValue;
        }

        // Handle boolean
        if (targetType == typeof(bool) || targetType == typeof(bool?))
        {
            if (bool.TryParse(value, out var boolValue))
                return boolValue;
        }

        return value;
    }

    private static Color? ParseNamedColor(string colorName)
    {
        return colorName.ToLowerInvariant() switch
        {
            "green" => Colors.Green,
            "gray" => Colors.Gray,
            "red" => Colors.Red,
            "blue" => Colors.Blue,
            "yellow" => Colors.Yellow,
            "orange" => Colors.Orange,
            "purple" => Colors.Purple,
            "pink" => Colors.Pink,
            "brown" => Colors.Brown,
            "black" => Colors.Black,
            "white" => Colors.White,
            "transparent" => Colors.Transparent,
            _ => null
        };
    }
}
