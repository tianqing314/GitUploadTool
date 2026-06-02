using System.Windows.Controls;
using GitUploadTool.ViewModels;

namespace GitUploadTool.Views;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
