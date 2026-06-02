using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using GitUploadTool.Models;
using GitUploadTool.Services;
using NLog;

namespace GitUploadTool.ViewModels;

public class UploadViewModel : BindableBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IGitHubService _gitHubService;
    private readonly IGitService _gitService;
    private readonly IRecentProjectService _recentProjectService;
    private readonly IGitIgnoreService _gitIgnoreService;
    private readonly ISettingsService _settingsService;
    private readonly IAuthenticationService _authService;
    private readonly ITokenService _tokenService;

    private string _projectPath = string.Empty;
    public string ProjectPath
    {
        get => _projectPath;
        set
        {
            if (SetProperty(ref _projectPath, value))
            {
                OnProjectPathChanged();
            }
        }
    }

    private string _projectName = string.Empty;
    public string ProjectName
    {
        get => _projectName;
        set => SetProperty(ref _projectName, value);
    }

    private int _fileCount;
    public int FileCount
    {
        get => _fileCount;
        set => SetProperty(ref _fileCount, value);
    }

    private string _projectSize = string.Empty;
    public string ProjectSize
    {
        get => _projectSize;
        set => SetProperty(ref _projectSize, value);
    }

    private bool _isGitRepo;
    public bool IsGitRepo
    {
        get => _isGitRepo;
        set => SetProperty(ref _isGitRepo, value);
    }

    private bool _repoExistsOnGithub;
    public bool RepoExistsOnGithub
    {
        get => _repoExistsOnGithub;
        set => SetProperty(ref _repoExistsOnGithub, value);
    }

    private GitHubRepository? _existingRepo;
    public GitHubRepository? ExistingRepo
    {
        get => _existingRepo;
        set => SetProperty(ref _existingRepo, value);
    }

    private string _selectedBranch = "main";
    public string SelectedBranch
    {
        get => _selectedBranch;
        set => SetProperty(ref _selectedBranch, value);
    }

    private string _commitMessage = string.Empty;
    public string CommitMessage
    {
        get => _commitMessage;
        set => SetProperty(ref _commitMessage, value);
    }

    private string _selectedGitIgnore = string.Empty;
    public string SelectedGitIgnore
    {
        get => _selectedGitIgnore;
        set => SetProperty(ref _selectedGitIgnore, value);
    }

    private List<string> _gitIgnoreTemplates = new();
    public List<string> GitIgnoreTemplates
    {
        get => _gitIgnoreTemplates;
        set => SetProperty(ref _gitIgnoreTemplates, value);
    }

    private int _currentStep;
    public int CurrentStep
    {
        get => _currentStep;
        set => SetProperty(ref _currentStep, value);
    }

    private bool _isUploading;
    public bool IsUploading
    {
        get => _isUploading;
        set => SetProperty(ref _isUploading, value);
    }

    private ObservableCollection<UploadProgress> _uploadProgress = new();
    public ObservableCollection<UploadProgress> UploadProgress
    {
        get => _uploadProgress;
        set => SetProperty(ref _uploadProgress, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private GitHubUser? _currentUser;
    public GitHubUser? CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    private bool _showCreateRepoDialog;
    public bool ShowCreateRepoDialog
    {
        get => _showCreateRepoDialog;
        set => SetProperty(ref _showCreateRepoDialog, value);
    }

    public ICommand BrowseFolderCommand { get; }
    public ICommand CheckRepoCommand { get; }
    public ICommand CreateRepoCommand { get; }
    public ICommand UploadCommand { get; }
    public ICommand NextStepCommand { get; }
    public ICommand PreviousStepCommand { get; }

    public UploadViewModel(
        IGitHubService gitHubService,
        IGitService gitService,
        IRecentProjectService recentProjectService,
        IGitIgnoreService gitIgnoreService,
        ISettingsService settingsService,
        IAuthenticationService authService,
        ITokenService tokenService)
    {
        _gitHubService = gitHubService;
        _gitService = gitService;
        _recentProjectService = recentProjectService;
        _gitIgnoreService = gitIgnoreService;
        _settingsService = settingsService;
        _authService = authService;
        _tokenService = tokenService;

        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        CheckRepoCommand = new RelayCommand(async () => await CheckRepoAsync());
        CreateRepoCommand = new RelayCommand(async () => await CreateRepoAsync());
        UploadCommand = new RelayCommand(async () => await UploadAsync());
        NextStepCommand = new RelayCommand(() => CurrentStep++);
        PreviousStepCommand = new RelayCommand(() => CurrentStep--);

        GitIgnoreTemplates = _gitIgnoreService.GetTemplates().Select(t => t.Name).ToList();
        LoadUserInfo();
    }

    private async void LoadUserInfo()
    {
        CurrentUser = await _authService.GetCurrentUserAsync();
        var settings = await _settingsService.GetSettingsAsync();
        SelectedBranch = settings.DefaultBranch;
    }

    private void BrowseFolder()
    {
        var dialog = new CommonOpenFileDialog
        {
            IsFolderPicker = true,
            Title = "Select Project Folder"
        };

        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            ProjectPath = dialog.FileName;
        }
    }

    private async void OnProjectPathChanged()
    {
        if (string.IsNullOrEmpty(ProjectPath))
            return;

        ProjectName = Path.GetFileName(ProjectPath);
        
        // Get project info
        var dirInfo = new DirectoryInfo(ProjectPath);
        FileCount = dirInfo.GetFiles("*", SearchOption.AllDirectories).Length;
        var totalSize = dirInfo.GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        ProjectSize = FormatFileSize(totalSize);

        // Check if git repo
        IsGitRepo = await _gitService.IsGitRepositoryAsync(ProjectPath);
        
        StatusMessage = $"Project loaded: {ProjectName}";
    }

    private async Task CheckRepoAsync()
    {
        if (string.IsNullOrEmpty(ProjectName) || CurrentUser == null)
            return;

        StatusMessage = "Checking repository on GitHub...";
        
        var exists = await _gitHubService.RepositoryExistsAsync(CurrentUser.Login, ProjectName);
        RepoExistsOnGithub = exists;
        
        if (exists)
        {
            ExistingRepo = await _gitHubService.GetRepositoryAsync(CurrentUser.Login, ProjectName);
            StatusMessage = $"Repository found: {ExistingRepo?.FullName}";
        }
        else
        {
            StatusMessage = "Repository not found on GitHub. You can create it.";
        }
    }

    private async Task CreateRepoAsync()
    {
        // Show dialog
        ShowCreateRepoDialog = true;
    }

    public async Task OnCreateRepoDialogClosed(bool confirmed, string name, string description, bool isPrivate)
    {
        ShowCreateRepoDialog = false;

        if (confirmed)
        {
            StatusMessage = "Creating repository...";
            var repo = await _gitHubService.CreateRepositoryAsync(name, description, isPrivate);
            
            if (repo != null)
            {
                ExistingRepo = repo;
                RepoExistsOnGithub = true;
                StatusMessage = $"Repository created: {repo.HtmlUrl}";
                CommitMessage = description ?? $"Initial commit for {name}";
            }
            else
            {
                StatusMessage = "Failed to create repository";
            }
        }
    }

    private async Task UploadAsync()
    {
        if (string.IsNullOrEmpty(ProjectPath) || ExistingRepo == null)
            return;

        IsUploading = true;
        UploadProgress.Clear();
        StatusMessage = "Uploading project...";

        var progress = new Progress<UploadProgress>(p =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                UploadProgress.Add(p);
                StatusMessage = p.Message;
            });
        });

        var commitMsg = string.IsNullOrEmpty(CommitMessage) 
            ? (ExistingRepo.Description ?? $"Update from GitUploadTool")
            : CommitMessage;

        // Apply .gitignore if selected
        if (!string.IsNullOrEmpty(SelectedGitIgnore))
        {
            var template = _gitIgnoreService.GetTemplates().FirstOrDefault(t => t.Name == SelectedGitIgnore);
            if (template != null)
            {
                await _gitIgnoreService.ApplyTemplateAsync(ProjectPath, template.Language);
            }
        }

        // Get token for authentication
        var token = await _tokenService.GetTokenAsync();
        
        var steps = await _gitService.UploadProjectAsync(
            ProjectPath,
            ExistingRepo.HtmlUrl,
            SelectedBranch,
            commitMsg,
            token,
            progress);

        var lastStep = steps.LastOrDefault();
        if (lastStep?.Status == StepStatus.Success)
        {
            StatusMessage = "Upload completed successfully!";
            
            // Save to recent projects
            await _recentProjectService.AddRecentProjectAsync(new RecentProject
            {
                Name = ProjectName,
                Path = ProjectPath,
                RepoUrl = ExistingRepo.HtmlUrl,
                UploadTime = DateTime.Now,
                Branch = SelectedBranch
            });
        }
        else
        {
            StatusMessage = $"Upload failed: {lastStep?.ErrorMessage}";
        }

        IsUploading = false;
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}