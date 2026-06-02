using System.Windows.Controls;
using GitUploadTool.ViewModels;

namespace GitUploadTool.Views;

public partial class AboutView : UserControl
{
    public AboutView(AboutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}