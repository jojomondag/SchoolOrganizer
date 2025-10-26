using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SchoolOrganizer.Src.Views.Windows.ImageCrop;

public partial class ImageHistoryCard : UserControl
{
    public static readonly StyledProperty<string> ImagePathProperty =
        AvaloniaProperty.Register<ImageHistoryCard, string>(nameof(ImagePath));

    public static readonly StyledProperty<bool> HasImageProperty =
        AvaloniaProperty.Register<ImageHistoryCard, bool>(nameof(HasImage));

    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<ImageHistoryCard, bool>(nameof(HasError));

    public string ImagePath
    {
        get => GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public bool HasImage
    {
        get => GetValue(HasImageProperty);
        set => SetValue(HasImageProperty, value);
    }

    public bool HasError
    {
        get => GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    public event EventHandler<string>? ImageSelected;
    public event EventHandler<string>? ImageDeleted;

    public ImageHistoryCard()
    {
        InitializeComponent();
        
        // Initialize properties
        HasImage = false;
        HasError = true; // Start with error state until image loads
        
        // Handle delete button click
        DeleteButton.Click += (s, e) =>
        {
            e.Handled = true;
            ImageDeleted?.Invoke(this, ImagePath);
        };

        // Handle card click (but not delete button)
        this.PointerPressed += OnCardPointerPressed;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == ImagePathProperty)
        {
            System.Diagnostics.Debug.WriteLine($"ImagePath property changed to: {ImagePath}");
            _ = Task.Run(async () =>
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await LoadImageAsync();
                });
            });
        }
    }

    private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        
        var pt = e.GetPosition(this);
        var deleteButtonRect = new Rect(this.Bounds.Width - 20, 2, 18, 18);
        
        if (deleteButtonRect.Contains(pt)) return;
        
        ImageSelected?.Invoke(this, ImagePath);
        e.Handled = true;
    }

    public async Task LoadImageAsync()
    {
        System.Diagnostics.Debug.WriteLine($"LoadImageAsync called for path: {ImagePath}");
        
        if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
        {
            System.Diagnostics.Debug.WriteLine($"Image path invalid or file doesn't exist: {ImagePath}");
            HasImage = false;
            HasError = true;
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"Loading image from: {ImagePath}");
            Bitmap bmp;
            using (var fs = File.OpenRead(ImagePath))
            using (var ms = new MemoryStream())
            {
                await fs.CopyToAsync(ms);
                ms.Position = 0;
                bmp = new Bitmap(ms);
            }
            
            CardImage.Source = bmp;
            HasImage = true;
            HasError = false;
            System.Diagnostics.Debug.WriteLine($"Image loaded successfully: {ImagePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading image {ImagePath}: {ex.Message}");
            HasImage = false;
            HasError = true;
        }
    }
}
