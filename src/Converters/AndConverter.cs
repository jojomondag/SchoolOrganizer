using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Data;
using System.Linq;
using System.Collections.Generic;

namespace SchoolOrganizer.Src.Converters;

public class AndConverter : IMultiValueConverter
{
    public static readonly AndConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (targetType != typeof(bool) && targetType != typeof(bool?))
        {
            return new BindingNotification(new InvalidCastException("Target type must be bool."), BindingErrorType.Error);
        }

        if (values == null || values.Count == 0)
        {
            return false;
        }

        foreach (var value in values)
        {
            if (value is bool b)
            {
                if (!b) return false;
            }
            else
            {
                return false;
            }
        }
        return true;
    }
}


