using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SchoolOrganizer.Models;

namespace SchoolOrganizer.Views;

public class StudentSelectionBrushConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count != 2) return new SolidColorBrush(Colors.Transparent);
        
        var selectedStudent = values[0] as Student;
        var currentStudent = values[1] as Student;
        
        if (selectedStudent != null && currentStudent != null && selectedStudent.Id == currentStudent.Id)
        {
            return new SolidColorBrush(Color.Parse("#27ae60")); // Green for selected
        }
        
        return new SolidColorBrush(Colors.Transparent);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
