using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SchoolOrganizer.Src.Converters;

/// <summary>
/// Specialized converter for value comparisons.
/// Supports various comparison operators for numeric and string values.
/// 
/// Parameter format: "operator,value" (e.g., "==,-1", "!=,-1", ">,5", "<=,10")
/// Supported operators: ==, !=, >, <, >=, <=
/// </summary>
public class ComparisonConverter : IValueConverter
{
    public static readonly ComparisonConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string param || string.IsNullOrWhiteSpace(param))
            return false;

        var parts = param.Split(',');
        if (parts.Length != 2)
            return false;

        var operatorStr = parts[0].Trim();
        var compareValueStr = parts[1].Trim();

        // Try to parse as numeric comparison first
        if (TryNumericComparison(value, operatorStr, compareValueStr, out var numericResult))
            return numericResult;

        // Fall back to string comparison
        return StringComparison(value, operatorStr, compareValueStr);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static bool TryNumericComparison(object? value, string operatorStr, string compareValueStr, out bool result)
    {
        result = false;

        // Try to parse both values as numbers
        if (value is int intValue && int.TryParse(compareValueStr, out var intCompareValue))
        {
            result = operatorStr switch
            {
                "==" => intValue == intCompareValue,
                "!=" => intValue != intCompareValue,
                ">" => intValue > intCompareValue,
                "<" => intValue < intCompareValue,
                ">=" => intValue >= intCompareValue,
                "<=" => intValue <= intCompareValue,
                _ => false
            };
            return true;
        }

        if (value is double doubleValue && double.TryParse(compareValueStr, out var doubleCompareValue))
        {
            result = operatorStr switch
            {
                "==" => doubleValue == doubleCompareValue,
                "!=" => doubleValue != doubleCompareValue,
                ">" => doubleValue > doubleCompareValue,
                "<" => doubleValue < doubleCompareValue,
                ">=" => doubleValue >= doubleCompareValue,
                "<=" => doubleValue <= doubleCompareValue,
                _ => false
            };
            return true;
        }

        return false;
    }

    private static bool StringComparison(object? value, string operatorStr, string compareValueStr)
    {
        var valueStr = value?.ToString() ?? string.Empty;

        return operatorStr switch
        {
            "==" => valueStr == compareValueStr,
            "!=" => valueStr != compareValueStr,
            _ => false
        };
    }
}
