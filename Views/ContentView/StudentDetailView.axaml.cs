using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SchoolOrganizer.ViewModels;

namespace SchoolOrganizer.Views.ContentView;

public partial class StudentDetailView : Window
{
    public StudentDetailView()
    {
        InitializeComponent();
    }

    private void OnFileTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not FileItem file)
            return;

        if (DataContext is StudentDetailViewModel viewModel)
        {
            viewModel.ViewFileCommand.Execute(file);
        }
    }
}
