using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace SchoolOrganizer.Src.Converters;

/// <summary>
/// Multi-value converter that returns true if any of the bound boolean values are true (OR logic)
/// </summary>
public class OrConverter : IMultiValueConverter
{
    public static readonly OrConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Count == 0)
            return false;

        // Return true if any value is true
        return values.OfType<bool>().Any(b => b);
    }
}

