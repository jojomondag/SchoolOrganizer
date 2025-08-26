using System;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Media;

namespace SchoolOrganizer.Utils;

public static class ImageTestHelper
{
    public static void CreateTestImages()
    {
        var profileImagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "ProfileImages");
        Directory.CreateDirectory(profileImagesPath);

        try
        {
            // Create simple test images with different colors
            CreateTestBitmap(Path.Combine(profileImagesPath, "red-square.png"), Colors.Red);
            CreateTestBitmap(Path.Combine(profileImagesPath, "blue-square.png"), Colors.Blue);
            CreateTestBitmap(Path.Combine(profileImagesPath, "green-square.png"), Colors.Green);
            
            System.Diagnostics.Debug.WriteLine("Test images created successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating test images: {ex.Message}");
        }
    }

    private static void CreateTestBitmap(string path, Color color)
    {
        const int size = 200;
        
        // Create a simple bitmap with Avalonia
        using var renderTargetBitmap = new RenderTargetBitmap(new Avalonia.PixelSize(size, size));
        using var context = renderTargetBitmap.CreateDrawingContext();
        
        // Fill with color
        context.FillRectangle(new SolidColorBrush(color), new Avalonia.Rect(0, 0, size, size));
        
        // Add some text
        var text = new FormattedText(
            "Test",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            24,
            new SolidColorBrush(Colors.White));
            
        context.DrawText(text, new Avalonia.Point(size / 2 - 20, size / 2 - 12));
        
        renderTargetBitmap.Save(path);
    }
}
