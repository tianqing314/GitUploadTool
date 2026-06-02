using System.Windows.Input;

namespace GitUploadTool.ViewModels;

public class CreateRepoDialogViewModel : BindableBase
{
    public string Title => "Create GitHub Repository";

    private string _repositoryName = string.Empty;
    public string RepositoryName
    {
        get => _repositoryName;
        set => SetProperty(ref _repositoryName, value);
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private bool _isPrivate;
    public bool IsPrivate
    {
        get => _isPrivate;
        set => SetProperty(ref _isPrivate, value);
    }

    public ICommand OkCommand { get; }
    public ICommand CancelCommand { get; }

    public event Action<bool>? DialogClosed;

    public CreateRepoDialogViewModel()
    {
        OkCommand = new RelayCommand(() =>
        {
            DialogClosed?.Invoke(true);
        });

        CancelCommand = new RelayCommand(() =>
        {
            DialogClosed?.Invoke(false);
        });
    }

    public void Initialize(string projectName)
    {
        RepositoryName = projectName;
    }
}