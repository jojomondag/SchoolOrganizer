using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace SchoolOrganizer.Views.Converters;

public class ExpandCollapseIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
        {
            return isExpanded ? MaterialIconKind.MenuDown : MaterialIconKind.MenuUp;
        }
        return MaterialIconKind.MenuUp;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
