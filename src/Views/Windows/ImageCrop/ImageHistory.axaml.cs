using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace SchoolOrganizer.Src.Views.Windows.ImageCrop;

public partial class ImageHistory : UserControl
{
    private ItemsControl? _itemsControl;
    private ObservableCollection<string> _imagePaths = new();

    public event EventHandler<string>? ImageSelected;
    public event EventHandler<string>? ImageDeleted;

    public ImageHistory()
    {
        InitializeComponent();
        _itemsControl = this.FindControl<ItemsControl>("ImageGalleryItemsControl");
        
        if (_itemsControl != null)
        {
            _itemsControl.ItemsSource = _imagePaths;
        }
    }

    public void OnCardImageSelected(object? sender, string imagePath)
    {
        ImageSelected?.Invoke(this, imagePath);
    }

    public void OnCardImageDeleted(object? sender, string imagePath)
    {
        ImageDeleted?.Invoke(this, imagePath);
        _imagePaths.Remove(imagePath);
    }

    public async Task LoadGalleryAsync(string[] imagePaths, Func<string, Task<object?>>? cropSettingsProvider = null)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Don't clear existing images - this should accumulate history
            if (imagePaths == null || imagePaths.Length == 0)
            {
                return;
            }

            // Add new images that aren't already in the history
            foreach (var path in imagePaths.Where(System.IO.File.Exists))
            {
                if (!_imagePaths.Contains(path))
                {
                    _imagePaths.Add(path);
                }
            }
        });

        // Load images asynchronously
        await LoadImagesAsync();
    }

    public void AddImageToHistory(string imagePath)
    {
        System.Diagnostics.Debug.WriteLine($"AddImageToHistory called with: {imagePath}");
        System.Diagnostics.Debug.WriteLine($"Current history count: {_imagePaths.Count}");
        
        if (!string.IsNullOrEmpty(imagePath) && System.IO.File.Exists(imagePath) && !_imagePaths.Contains(imagePath))
        {
            System.Diagnostics.Debug.WriteLine($"Adding new image to history: {imagePath}");
            _imagePaths.Add(imagePath);
            System.Diagnostics.Debug.WriteLine($"History count after adding: {_imagePaths.Count}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"Not adding image - already exists or invalid: {imagePath}");
        }
    }

    private async Task LoadImagesAsync()
    {
        // The ImageHistoryCard will automatically load images when ImagePath is set
        // due to the property change subscription in the constructor
        await Task.CompletedTask;
    }

    public void ClearGallery()
    {
        _imagePaths.Clear();
    }
}