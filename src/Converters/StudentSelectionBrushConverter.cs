using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SchoolOrganizer.Src.Models.Students;

namespace SchoolOrganizer.Src.Converters;

public class StudentSelectionBrushConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count != 2 || values[0] is not Student selectedStudent || values[1] is not Student currentStudent)
            return new SolidColorBrush(Colors.Transparent);
        
        return selectedStudent.Id == currentStudent.Id 
            ? new SolidColorBrush((Color)Application.Current!.Resources["SuccessColor"]!)
            : new SolidColorBrush(Colors.Transparent);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
