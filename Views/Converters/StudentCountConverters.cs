using System;
using System.Globalization;
using Avalonia.Data.Converters;
using System.Collections;

namespace SchoolOrganizer.Views.Converters;

public class StudentCountToCardSizeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ICollection collection)
        {
            var count = collection.Count;
            
            // Return different sizes based on count
            return count switch
            {
                <= 4 => 360.0,    // Expanded
                <= 8 => 300.0,    // Detailed  
                <= 16 => 240.0,   // Standard
                _ => 180.0         // Compact
            };
        }
        
        return 240.0; // Default size
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StudentCountToImageSizeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ICollection collection)
        {
            var count = collection.Count;
            
            // Return different image sizes based on count
            return count switch
            {
                <= 4 => 150.0,    // Expanded
                <= 8 => 120.0,    // Detailed  
                <= 16 => 90.0,    // Standard
                _ => 70.0          // Compact
            };
        }
        
        return 90.0; // Default size
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StudentCountToFontSizeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ICollection collection)
        {
            var count = collection.Count;
            var sizeType = parameter?.ToString() ?? "name";
            
            return (sizeType, count) switch
            {
                ("name", <= 4) => 20.0,    // Expanded
                ("name", <= 8) => 18.0,    // Detailed
                ("name", <= 16) => 16.0,   // Standard
                ("name", _) => 14.0,       // Compact
                
                ("role", <= 4) => 16.0,    // Expanded
                ("role", <= 8) => 14.0,    // Detailed
                ("role", <= 16) => 12.0,   // Standard
                ("role", _) => 10.0,       // Compact
                
                ("secondary", <= 4) => 14.0,  // Expanded
                ("secondary", <= 8) => 12.0,  // Detailed
                ("secondary", <= 16) => 10.0, // Standard
                ("secondary", _) => 9.0,      // Compact
                
                _ => 16.0
            };
        }
        
        return 16.0; // Default size
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StudentCountToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ICollection collection)
        {
            var count = collection.Count;
            var elementType = parameter?.ToString() ?? "";
            
            return (elementType, count) switch
            {
                ("email", <= 8) => true,      // Show email for Expanded and Detailed
                ("enrollment", <= 4) => true, // Show enrollment only for Expanded
                _ => false
            };
        }
        
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}