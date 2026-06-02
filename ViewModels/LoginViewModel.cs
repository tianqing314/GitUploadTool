using System.Windows.Input;
using GitUploadTool.Services;
using NLog;

namespace GitUploadTool.ViewModels;

public class LoginViewModel : BindableBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IAuthenticationService _authService;
    private readonly ITokenService _tokenService;

    public event EventHandler? LoginSucceeded;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand LoginCommand { get; }
    public ICommand OpenHelpCommand { get; }

    public LoginViewModel(IAuthenticationService authService, ITokenService tokenService)
    {
        _authService = authService;
        _tokenService = tokenService;
        LoginCommand = new RelayCommand<string>(async (token) => await LoginAsync(token));
        OpenHelpCommand = new RelayCommand(OpenHelpPage);
    }

    private async Task LoginAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            StatusMessage = "请输入您的个人访问令牌";
            return;
        }

        IsLoading = true;
        StatusMessage = "正在验证令牌...";

        try
        {
            // Save token first
            await _tokenService.SaveTokenAsync(token.Trim());

            // Verify token by getting user info
            var user = await _authService.GetCurrentUserAsync();
            if (user != null)
            {
                StatusMessage = $"欢迎，{user.Login}！";
                Logger.Info($"User {user.Login} logged in successfully");
                await Task.Delay(500); // Brief delay to show success message
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // Remove invalid token
                await _tokenService.DeleteTokenAsync();
                StatusMessage = "令牌无效，请检查后重试。";
                Logger.Warn("Login failed - invalid token");
            }
        }
        catch (Exception ex)
        {
            await _tokenService.DeleteTokenAsync();
            StatusMessage = $"Error: {ex.Message}";
            Logger.Error(ex, "Login error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenHelpPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/settings/tokens/new?scopes=repo,user&description=GitUploadTool",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open help page");
        }
    }
}

public class RelayCommand : ICommand
{
    private readonly Func<Task> _executeAsync;
    private readonly Func<bool> _canExecute;
    private readonly Action _execute;

    public RelayCommand(Action execute, Func<bool> canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public event EventHandler CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object parameter)
    {
        if (_executeAsync != null)
            await _executeAsync();
        else
            _execute?.Invoke();
    }
}