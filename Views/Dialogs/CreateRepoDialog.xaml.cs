using System.Windows.Controls;
using GitUploadTool.ViewModels;

namespace GitUploadTool.Views.Dialogs;

public partial class CreateRepoDialog : UserControl
{
    public CreateRepoDialog(CreateRepoDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}