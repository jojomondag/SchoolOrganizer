using System;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;

namespace SchoolOrganizer.Utils;

public static class TestImageGenerator
{
    public static void CreateTestImages()
    {
        var profileImagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "ProfileImages");
        Directory.CreateDirectory(profileImagesPath);

        // Create a simple colored rectangle as test image
        CreateColoredImage(Path.Combine(profileImagesPath, "test-red.png"), SKColors.Red);
        CreateColoredImage(Path.Combine(profileImagesPath, "test-blue.png"), SKColors.Blue);
        CreateColoredImage(Path.Combine(profileImagesPath, "test-green.png"), SKColors.Green);
    }

    private static void CreateColoredImage(string filePath, SKColor color)
    {
        const int width = 200;
        const int height = 200;

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        
        canvas.Clear(color);
        
        // Add some text to make it more interesting
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 24,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.Default
        };
        
        canvas.DrawText("Test Image", width / 2, height / 2, paint);
        
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);
    }
}
