using System.Windows;
using GitUploadTool.ViewModels;

namespace GitUploadTool.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}