using System.Windows.Input;
using GitUploadTool.Models;
using GitUploadTool.Services;
using NLog;

namespace GitUploadTool.ViewModels;

public class SettingsViewModel : BindableBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ISettingsService _settingsService;
    private readonly ITokenService _tokenService;
    private readonly IGitIgnoreService _gitIgnoreService;

    private string? _proxyAddress;
    public string? ProxyAddress
    {
        get => _proxyAddress;
        set => SetProperty(ref _proxyAddress, value);
    }

    private int? _proxyPort;
    public int? ProxyPort
    {
        get => _proxyPort;
        set => SetProperty(ref _proxyPort, value);
    }

    private string _defaultBranch = "main";
    public string DefaultBranch
    {
        get => _defaultBranch;
        set => SetProperty(ref _defaultBranch, value);
    }

    private string _defaultCommitMessage = "Update from GitUploadTool";
    public string DefaultCommitMessage
    {
        get => _defaultCommitMessage;
        set => SetProperty(ref _defaultCommitMessage, value);
    }

    private bool _hasToken;
    public bool HasToken
    {
        get => _hasToken;
        set => SetProperty(ref _hasToken, value);
    }

    private List<GitIgnoreTemplate> _gitIgnoreTemplates = new();
    public List<GitIgnoreTemplate> GitIgnoreTemplates
    {
        get => _gitIgnoreTemplates;
        set => SetProperty(ref _gitIgnoreTemplates, value);
    }

    private GitIgnoreTemplate? _selectedTemplate;
    public GitIgnoreTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set => SetProperty(ref _selectedTemplate, value);
    }

    private string _templatePreview = string.Empty;
    public string TemplatePreview
    {
        get => _templatePreview;
        set => SetProperty(ref _templatePreview, value);
    }

    public ICommand SaveSettingsCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand ClearTokenCommand { get; }

    public SettingsViewModel(
        ISettingsService settingsService,
        ITokenService tokenService,
        IGitIgnoreService gitIgnoreService)
    {
        _settingsService = settingsService;
        _tokenService = tokenService;
        _gitIgnoreService = gitIgnoreService;

        SaveSettingsCommand = new RelayCommand(async () => await SaveSettingsAsync());
        LogoutCommand = new RelayCommand(async () => await LogoutAsync());
        ClearTokenCommand = new RelayCommand(async () => await ClearTokenAsync());

        GitIgnoreTemplates = _gitIgnoreService.GetTemplates();
        LoadSettings();
    }

    private async void LoadSettings()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            ProxyAddress = settings.ProxyAddress;
            ProxyPort = settings.ProxyPort;
            DefaultBranch = settings.DefaultBranch;
            DefaultCommitMessage = settings.DefaultCommitMessage;
            HasToken = await _tokenService.HasTokenAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load settings");
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new AppSettings
            {
                ProxyAddress = ProxyAddress,
                ProxyPort = ProxyPort,
                DefaultBranch = DefaultBranch,
                DefaultCommitMessage = DefaultCommitMessage
            };
            await _settingsService.SaveSettingsAsync(settings);
            Logger.Info("Settings saved");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save settings");
        }
    }

    private async Task LogoutAsync()
    {
        await _tokenService.DeleteTokenAsync();
        HasToken = false;
    }

    private async Task ClearTokenAsync()
    {
        await _tokenService.DeleteTokenAsync();
        HasToken = false;
    }
}
