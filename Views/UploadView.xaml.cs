using System.Windows;
using System.Windows.Controls;
using GitUploadTool.ViewModels;

namespace GitUploadTool.Views;

public partial class UploadView : UserControl
{
    private readonly UploadViewModel _viewModel;

    public UploadView(UploadViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void CancelCreateRepo_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OnCreateRepoDialogClosed(false, null, null, false);
    }

    private void ConfirmCreateRepo_Click(object sender, RoutedEventArgs e)
    {
        var name = FindName("RepoNameBox") is TextBox nameBox ? nameBox.Text : _viewModel.ProjectName;
        var description = FindName("RepoDescBox") is TextBox descBox ? descBox.Text : "";
        var isPrivate = FindName("PrivateCheck") is CheckBox checkBox && checkBox.IsChecked == true;
        
        _viewModel.OnCreateRepoDialogClosed(true, name, description, isPrivate);
    }
}
