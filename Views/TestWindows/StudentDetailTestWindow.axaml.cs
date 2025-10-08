using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Material.Icons.Avalonia;

namespace SchoolOrganizer.Views.TestWindows;

/// <summary>
/// Test window for quickly testing StudentDetailView UI changes
/// </summary>
public partial class StudentDetailTestWindow : Window
{
    private bool _isExplorerCollapsed = false;

    public StudentDetailTestWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnToggleExplorerClick(object sender, RoutedEventArgs e)
    {
        _isExplorerCollapsed = !_isExplorerCollapsed;
        
        // Find the main grid and access its column definitions
        var mainGrid = this.FindControl<Grid>("MainGrid");
        var explorerPanel = this.FindControl<Border>("ExplorerPanel");
        var mainContentPanel = this.FindControl<Border>("MainContentPanel");
        var toggleButton = this.FindControl<Button>("ToggleExplorerButton");
        var toggleIcon = this.FindControl<MaterialIcon>("ToggleIcon");
        
        if (mainGrid != null && explorerPanel != null && mainContentPanel != null && toggleButton != null)
        {
            var explorerColumn = mainGrid.ColumnDefinitions[0]; // First column
            
            if (_isExplorerCollapsed)
            {
                // Collapse the explorer
                explorerColumn.Width = new GridLength(0);
                explorerPanel.IsVisible = false;
                
                // Expand main content to use full width
                Grid.SetColumnSpan(mainContentPanel, 3); // Span all 3 columns
                Grid.SetColumn(mainContentPanel, 0); // Start from first column
                
                // Move button to left border of the window (Canvas.Left = 0)
                Canvas.SetLeft(toggleButton, 0);
                
                if (toggleIcon != null)
                    toggleIcon.Kind = Material.Icons.MaterialIconKind.ChevronRight;
            }
            else
            {
                // Expand the explorer
                explorerColumn.Width = new GridLength(300, GridUnitType.Pixel);
                explorerPanel.IsVisible = true;
                
                // Reset main content to normal position
                Grid.SetColumnSpan(mainContentPanel, 1); // Span only 1 column
                Grid.SetColumn(mainContentPanel, 2); // Start from third column
                
                // Move button to the splitter position (Canvas.Left = 300)
                Canvas.SetLeft(toggleButton, 300);
                
                if (toggleIcon != null)
                    toggleIcon.Kind = Material.Icons.MaterialIconKind.ChevronLeft;
            }
        }
    }
}
