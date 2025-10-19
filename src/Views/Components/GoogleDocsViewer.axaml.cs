using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SchoolOrganizer.Src.Models.Assignments;
using Serilog;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SchoolOrganizer.Src.Views.Components
{
    public partial class GoogleDocsViewer : UserControl
    {
        private StackPanel? _documentPreviewContainer;
        private StudentFile? _currentFile;

        public GoogleDocsViewer()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _documentPreviewContainer = this.FindControl<StackPanel>("DocumentPreviewContainer");
        }

        private async void OnDataContextChanged(object? sender, EventArgs e)
        {
            Log.Information("GoogleDocsViewer OnDataContextChanged called - DataContext: {DataContextType}", 
                DataContext?.GetType().Name ?? "null");

            // Unsubscribe from previous file
            if (_currentFile != null)
            {
                _currentFile.PropertyChanged -= OnStudentFilePropertyChanged;
                _currentFile = null;
            }

            if (DataContext is StudentFile file)
            {
                Log.Information("GoogleDocsViewer DataContext changed - File: {FileName}, IsGoogleDoc: {IsGoogleDoc}",
                    file.FileName, file.IsGoogleDoc);

                if (file.IsGoogleDoc)
                {
                    _currentFile = file;

                    // Subscribe to property changes
                    file.PropertyChanged += OnStudentFilePropertyChanged;

                    // Always load preview for Google Docs
                    Log.Information("Starting to load document preview for {FileName}", file.FileName);
                    await LoadDocumentPreviewAsync(file);
                }
                else
                {
                    Log.Warning("GoogleDocsViewer - File is not marked as Google Doc: {FileName}, IsGoogleDoc: {IsGoogleDoc}",
                        file.FileName, file.IsGoogleDoc);
                }
            }
            else
            {
                Log.Warning("GoogleDocsViewer DataContext is not a StudentFile. Type: {Type}",
                    DataContext?.GetType().Name ?? "null");
            }
        }

        private void OnStudentFilePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Reserved for future property change handling
        }

        private async System.Threading.Tasks.Task LoadDocumentPreviewAsync(StudentFile file)
        {
            Log.Information("LoadDocumentPreviewAsync called for {FileName}", file.FileName);

            if (_documentPreviewContainer == null)
            {
                Log.Warning("_documentPreviewContainer is null!");
                return;
            }

            try
            {
                // Show loading message
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _documentPreviewContainer?.Children.Clear();
                    _documentPreviewContainer?.Children.Add(new TextBlock
                    {
                        Text = "Loading document preview...",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontSize = 12,
                        Foreground = Avalonia.Media.Brushes.Gray,
                        FontStyle = Avalonia.Media.FontStyle.Italic
                    });
                });

                Log.Information("Checking if file exists: {FilePath}", file.FilePath);

                // Check if the downloaded file exists
                if (!File.Exists(file.FilePath))
                {
                    Log.Warning("Downloaded file not found: {FilePath}", file.FilePath);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _documentPreviewContainer?.Children.Clear();
                        _documentPreviewContainer?.Children.Add(new TextBlock
                        {
                            Text = "Downloaded file not found. Please download the assignment again.",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            FontSize = 12,
                            Foreground = Avalonia.Media.Brushes.Red
                        });
                    });
                    return;
                }

                Log.Information("File exists, checking extension...");

                var extension = Path.GetExtension(file.FilePath).ToLowerInvariant();

                if (extension == ".docx")
                {
                    await ExtractDocxContentWithImagesAsync(file.FilePath);
                }
                else
                {
                    string message = extension switch
                    {
                        ".xlsx" => "📊 Excel Spreadsheet\n\nThis is a Microsoft Excel file. Click 'Open in External App' to view it in Excel or a spreadsheet application.",
                        ".pptx" => "📽️ PowerPoint Presentation\n\nThis is a Microsoft PowerPoint presentation. Click 'Open in External App' to view it in PowerPoint.",
                        _ => $"This file type ({extension}) cannot be previewed. Click 'Open in External App' to view it."
                    };

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _documentPreviewContainer?.Children.Clear();
                        _documentPreviewContainer?.Children.Add(new TextBlock
                        {
                            Text = message,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            FontSize = 12
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading document preview for {FileName}", file.FileName);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _documentPreviewContainer?.Children.Clear();
                    _documentPreviewContainer?.Children.Add(new TextBlock
                    {
                        Text = $"Error loading preview: {ex.Message}\n\nClick 'Open in External App' to view the file.",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontSize = 12,
                        Foreground = Avalonia.Media.Brushes.Red
                    });
                });
            }
        }

        private async System.Threading.Tasks.Task ExtractDocxContentWithImagesAsync(string filePath)
        {
            // Extract data on background thread (no UI elements created here)
            var contentData = await System.Threading.Tasks.Task.Run(() =>
            {
                var items = new List<DocumentContentItem>();

                try
                {
                    using var doc = WordprocessingDocument.Open(filePath, false);
                    var body = doc.MainDocumentPart?.Document?.Body;

                    if (body == null)
                    {
                        return null;
                    }

                    Log.Information("Processing document body with {ChildCount} children", body.ChildElements.Count);

                    // Process document body children in order
                    foreach (var element in body.ChildElements)
                    {
                        if (element is Paragraph paragraph)
                        {
                            Log.Debug("Found paragraph");

                            // Check if paragraph contains an image (Drawing element)
                            var drawing = paragraph.Descendants<DocumentFormat.OpenXml.Wordprocessing.Drawing>().FirstOrDefault();
                            if (drawing != null)
                            {
                                Log.Information("Found Drawing element in paragraph");
                                // Extract image bitmap
                                var imageBitmap = ExtractImageFromDrawing(doc, drawing);
                                if (imageBitmap != null)
                                {
                                    items.Add(new DocumentContentItem { Type = ContentType.Image, ImageData = imageBitmap });
                                    Log.Information("Added image to content items");
                                }
                            }
                            else
                            {
                                // Check for VML images (older format)
                                var vmlImage = paragraph.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().FirstOrDefault();
                                if (vmlImage != null)
                                {
                                    Log.Information("Found VML ImageData in paragraph");
                                    var imageBitmap = ExtractVmlImage(doc, vmlImage);
                                    if (imageBitmap != null)
                                    {
                                        items.Add(new DocumentContentItem { Type = ContentType.Image, ImageData = imageBitmap });
                                        Log.Information("Added VML image to content items");
                                    }
                                }
                                else
                                {
                                    // Extract text
                                    var text = paragraph.InnerText;
                                    if (!string.IsNullOrWhiteSpace(text))
                                    {
                                        items.Add(new DocumentContentItem { Type = ContentType.Text, TextData = text });
                                        Log.Debug("Added text paragraph: {TextPreview}", text.Substring(0, Math.Min(50, text.Length)));
                                    }
                                }
                            }
                        }
                        else if (element is DocumentFormat.OpenXml.Wordprocessing.Table table)
                        {
                            Log.Debug("Found table");
                            items.Add(new DocumentContentItem { Type = ContentType.Table });
                        }
                        else
                        {
                            Log.Debug("Found element of type: {ElementType}", element.GetType().Name);
                        }
                    }

                    Log.Information("Extraction complete: Found {ItemCount} content items", items.Count);

                    return items;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting content from .docx file: {FilePath}", filePath);
                    return null;
                }
            });

            // Create UI elements on UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    _documentPreviewContainer?.Children.Clear();

                    if (contentData == null)
                    {
                        _documentPreviewContainer?.Children.Add(new TextBlock
                        {
                            Text = "Unable to read document content.",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            FontSize = 12,
                            Foreground = Avalonia.Media.Brushes.Red
                        });
                        return;
                    }

                    // Add header
                    _documentPreviewContainer?.Children.Add(new TextBlock
                    {
                        Text = "📄 Document Preview:",
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        FontSize = 14,
                        Margin = new Avalonia.Thickness(0, 0, 0, 8)
                    });

                    // Add content items
                    if (contentData.Count == 0)
                    {
                        _documentPreviewContainer?.Children.Add(new TextBlock
                        {
                            Text = "This document appears to be empty.",
                            FontStyle = Avalonia.Media.FontStyle.Italic,
                            FontSize = 12,
                            Foreground = Avalonia.Media.Brushes.Gray
                        });
                    }
                    else
                    {
                        foreach (var item in contentData)
                        {
                            switch (item.Type)
                            {
                                case ContentType.Text:
                                    _documentPreviewContainer?.Children.Add(new TextBlock
                                    {
                                        Text = item.TextData,
                                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                                        FontSize = 12,
                                        Margin = new Avalonia.Thickness(0, 4, 0, 4)
                                    });
                                    break;

                                case ContentType.Image:
                                    _documentPreviewContainer?.Children.Add(new Avalonia.Controls.Image
                                    {
                                        Source = item.ImageData,
                                        MaxWidth = 600,
                                        Stretch = Avalonia.Media.Stretch.Uniform,
                                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                                        Margin = new Avalonia.Thickness(0, 8, 0, 8)
                                    });
                                    break;

                                case ContentType.Table:
                                    _documentPreviewContainer?.Children.Add(new Avalonia.Controls.Border
                                    {
                                        Background = Avalonia.Media.Brushes.LightGray,
                                        BorderBrush = Avalonia.Media.Brushes.Gray,
                                        BorderThickness = new Avalonia.Thickness(1),
                                        CornerRadius = new Avalonia.CornerRadius(4),
                                        Padding = new Avalonia.Thickness(12),
                                        Margin = new Avalonia.Thickness(0, 8, 0, 8),
                                        Child = new TextBlock
                                        {
                                            Text = "📊 Table (cannot be previewed - open file to view)",
                                            FontStyle = Avalonia.Media.FontStyle.Italic,
                                            FontSize = 11
                                        }
                                    });
                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error creating UI elements for document preview");
                    _documentPreviewContainer?.Children.Clear();
                    _documentPreviewContainer?.Children.Add(new TextBlock
                    {
                        Text = $"Error displaying document: {ex.Message}",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontSize = 12,
                        Foreground = Avalonia.Media.Brushes.Red
                    });
                }
            });
        }

        private enum ContentType
        {
            Text,
            Image,
            Table
        }

        private class DocumentContentItem
        {
            public ContentType Type { get; set; }
            public string? TextData { get; set; }
            public Avalonia.Media.Imaging.Bitmap? ImageData { get; set; }
        }

        private Avalonia.Media.Imaging.Bitmap? ExtractVmlImage(WordprocessingDocument doc, DocumentFormat.OpenXml.Vml.ImageData vmlImage)
        {
            try
            {
                Log.Debug("Attempting to extract VML image");

                var imageId = vmlImage.RelationshipId?.Value;
                Log.Debug("Found VML image ID: {ImageId}", imageId ?? "null");

                if (string.IsNullOrEmpty(imageId))
                {
                    Log.Warning("VML Image ID is null or empty");
                    return null;
                }

                // Get the image part
                var imagePart = doc.MainDocumentPart?.GetPartById(imageId) as DocumentFormat.OpenXml.Packaging.ImagePart;
                if (imagePart == null)
                {
                    Log.Warning("ImagePart not found for VML ID: {ImageId}", imageId);
                    return null;
                }

                // Convert to Avalonia Bitmap - copy stream to MemoryStream first
                using var sourceStream = imagePart.GetStream();
                using var memoryStream = new MemoryStream();
                sourceStream.CopyTo(memoryStream);
                memoryStream.Position = 0; // Reset position for reading

                var bitmap = new Avalonia.Media.Imaging.Bitmap(memoryStream);
                Log.Information("Successfully extracted VML image: {Width}x{Height}", bitmap.PixelSize.Width, bitmap.PixelSize.Height);
                return bitmap;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to extract VML image");
                return null;
            }
        }

        private Avalonia.Media.Imaging.Bitmap? ExtractImageFromDrawing(WordprocessingDocument doc, DocumentFormat.OpenXml.Wordprocessing.Drawing drawing)
        {
            try
            {
                Log.Debug("Attempting to extract image from Drawing element");

                // Get the image reference
                var blip = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault();
                if (blip == null)
                {
                    Log.Warning("No Blip element found in Drawing");
                    return null;
                }

                var imageId = blip.Embed?.Value;
                Log.Debug("Found image ID: {ImageId}", imageId ?? "null");

                if (string.IsNullOrEmpty(imageId))
                {
                    Log.Warning("Image ID is null or empty");
                    return null;
                }

                // Get the image part
                var imagePart = doc.MainDocumentPart?.GetPartById(imageId) as DocumentFormat.OpenXml.Packaging.ImagePart;
                if (imagePart == null)
                {
                    Log.Warning("ImagePart not found for ID: {ImageId}", imageId);
                    return null;
                }

                // Convert to Avalonia Bitmap - copy stream to MemoryStream first
                using var sourceStream = imagePart.GetStream();
                using var memoryStream = new MemoryStream();
                sourceStream.CopyTo(memoryStream);
                memoryStream.Position = 0; // Reset position for reading

                var bitmap = new Avalonia.Media.Imaging.Bitmap(memoryStream);
                Log.Information("Successfully extracted image: {Width}x{Height}", bitmap.PixelSize.Width, bitmap.PixelSize.Height);
                return bitmap;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to extract image from drawing");
                return null;
            }
        }

        private async System.Threading.Tasks.Task ShowErrorMessage(string message)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _documentPreviewContainer?.Children.Clear();
                _documentPreviewContainer?.Children.Add(new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brushes.Red
                });
            });
        }
    }
}
