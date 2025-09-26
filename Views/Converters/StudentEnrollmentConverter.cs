using System;
using System.Globalization;
using Avalonia.Data.Converters;
using SchoolOrganizer.Models;

namespace SchoolOrganizer.Views.Converters;

public class StudentEnrollmentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Student student)
        {
            return student.EnrollmentDate;
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}