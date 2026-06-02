using System.Windows.Input;
using GitUploadTool.Models;
using GitUploadTool.Services;
using NLog;

namespace GitUploadTool.ViewModels;

public class HomeViewModel : BindableBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IRecentProjectService _recentProjectService;
    private readonly IGitHubService _gitHubService;
    private readonly IAuthenticationService _authService;

    private GitHubUser? _currentUser;
    public GitHubUser? CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    private List<RecentProject> _recentProjects = new();
    public List<RecentProject> RecentProjects
    {
        get => _recentProjects;
        set => SetProperty(ref _recentProjects, value);
    }

    private List<GitHubRepository> _repositories = new();
    public List<GitHubRepository> Repositories
    {
        get => _repositories;
        set => SetProperty(ref _repositories, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenUrlCommand { get; }

    public HomeViewModel(
        IRecentProjectService recentProjectService,
        IGitHubService gitHubService,
        IAuthenticationService authService)
    {
        _recentProjectService = recentProjectService;
        _gitHubService = gitHubService;
        _authService = authService;

        RefreshCommand = new RelayCommand(async () => await LoadDataAsync());
        OpenUrlCommand = new RelayCommand<string>(url =>
        {
            if (!string.IsNullOrEmpty(url))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        });

        LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            CurrentUser = await _authService.GetCurrentUserAsync();
            RecentProjects = await _recentProjectService.GetRecentProjectsAsync();
            Repositories = await _gitHubService.GetRepositoriesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load home data");
        }
        finally
        {
            IsLoading = false;
        }
    }
}