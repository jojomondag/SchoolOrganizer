using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Material.Icons;
using Material.Icons.Avalonia;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
namespace SchoolOrganizer.Views.Windows.ImageCrop;
public partial class ImageHistory : UserControl
{
    private const int ThumbSize = 90;
    private Grid? _galleryGrid;
    public event EventHandler<string>? ImageSelected;
    public event EventHandler<string>? ImageDeleted;
    public ImageHistory()
    {
        InitializeComponent();
        _galleryGrid = this.FindControl<Grid>("ImageGalleryGrid");
    }
    public async Task LoadGalleryAsync(string[] imagePaths, Func<string, Task<object?>>? cropSettingsProvider = null)
    {
        if (_galleryGrid == null) return;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _galleryGrid.Children.Clear();
            _galleryGrid.RowDefinitions.Clear();
            if (imagePaths == null || imagePaths.Length == 0)
            {
                var txt = new TextBlock
                {
                    Text = "No images in history",
                    FontSize = 12,
                    Foreground = GetBrush("TextSecondaryBrush", Colors.Gray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                Grid.SetRow(txt, 0);
                Grid.SetColumnSpan(txt, 2);
                _galleryGrid.Children.Add(txt);
                return;
            }
            var brushes = (
                Border: GetBrush("ImageCropperThumbnailBorderBrush", Colors.Black),
                BorderHover: GetBrush("ImageCropperThumbnailBorderHoverBrush", Color.FromRgb(44, 62, 80)),
                Background: GetBrush("ImageCropperThumbnailBackgroundBrush", Color.FromRgb(232, 244, 248)),
                Text: GetBrush("TextPrimaryBrush", Colors.Black)
            );
            int idx = 0;
            foreach (var path in imagePaths.Where(File.Exists))
            {
                int row = idx / 2;
                int col = idx % 2;
                while (_galleryGrid.RowDefinitions.Count <= row)
                    _galleryGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var container = new Grid();
                try
                {
                    Bitmap bmp;
                    using (var fs = File.OpenRead(path))
                    using (var ms = new MemoryStream())
                    {
                        fs.CopyTo(ms);
                        ms.Position = 0;
                        bmp = new Bitmap(ms);
                    }
                    container.Children.Add(new Image
                    {
                        Source = bmp,
                        Stretch = Stretch.UniformToFill
                    });
                    var delBtn = CreateDeleteButton();
                    var localPath = path;
                    delBtn.Click += (s, e) =>
                    {
                        e.Handled = true;
                        ImageDeleted?.Invoke(this, localPath);
                    };
                    delBtn.PointerPressed += (s, e) => e.Handled = true;
                    delBtn.PointerReleased += (s, e) => e.Handled = true;
                    container.Children.Add(delBtn);
                }
                catch
                {
                    container.Children.Add(new TextBlock
                    {
                        Text = "?",
                        FontSize = 20,
                        Foreground = brushes.Text,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
                var border = new Border
                {
                    Width = ThumbSize,
                    Height = ThumbSize,
                    CornerRadius = new CornerRadius(4),
                    ClipToBounds = true,
                    BorderBrush = brushes.Border,
                    BorderThickness = new Thickness(2),
                    Background = brushes.Background,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Child = container
                };
                var localPath2 = path;
                border.PointerPressed += (s, e) =>
                {
                    if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;
                    var pt = e.GetPosition(container);
                    if (new Rect(container.Bounds.Width - 19, 3, 16, 16).Contains(pt)) return;
                    ImageSelected?.Invoke(this, localPath2);
                    e.Handled = true;
                };
                Grid.SetRow(border, row);
                Grid.SetColumn(border, col);
                _galleryGrid.Children.Add(border);
                idx++;
            }
        });
    }
    public void ClearGallery()
    {
        if (_galleryGrid == null) return;
        _galleryGrid.Children.Clear();
        _galleryGrid.RowDefinitions.Clear();
    }
    private Button CreateDeleteButton()
    {
        var grid = new Grid
        {
            Width = 16,
            Height = 16
        };
        grid.Children.Add(new MaterialIcon
        {
            Kind = MaterialIconKind.Close,
            Width = 10,
            Height = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        return new Button
        {
            Content = grid,
            Width = 16,
            Height = 16,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 3, 3, 0),
            Cursor = new Cursor(StandardCursorType.Hand),
            ZIndex = 10,
            Classes = { "DeleteButton" },
            Foreground = GetBrush("IconBrush", Colors.Black)
        };
    }
    private IBrush GetBrush(string key, Color fallback)
    {
        return this.FindResource(key) as IBrush ?? new SolidColorBrush(fallback);
    }
}