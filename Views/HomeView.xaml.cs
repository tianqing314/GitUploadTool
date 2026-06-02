using System.Windows.Controls;
using GitUploadTool.ViewModels;

namespace GitUploadTool.Views;

public partial class HomeView : UserControl
{
    public HomeView(HomeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
