using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using GitUploadTool.Models;
using GitUploadTool.Services;
using NLog;

namespace GitUploadTool.ViewModels;

public class MainWindowViewModel : BindableBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IAuthenticationService _authService;
    private readonly IServiceProvider _serviceProvider;

    private GitHubUser? _currentUser;
    public GitHubUser? CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    private UserControl _currentView;
    public UserControl CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    private string _selectedView = "HomeView";
    public string SelectedView
    {
        get => _selectedView;
        set
        {
            if (SetProperty(ref _selectedView, value))
            {
                NavigateToView(value);
            }
        }
    }

    public ICommand NavigateCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand OpenUrlCommand { get; }

    public MainWindowViewModel(IAuthenticationService authService, IServiceProvider serviceProvider)
    {
        _authService = authService;
        _serviceProvider = serviceProvider;

        NavigateCommand = new RelayCommand<string>(viewName =>
        {
            SelectedView = viewName;
        });

        LogoutCommand = new RelayCommand(async () => await LogoutAsync());

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

        LoadUserInfo();
        NavigateToView("HomeView");
    }

    private async void LoadUserInfo()
    {
        try
        {
            CurrentUser = await _authService.GetCurrentUserAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load user info");
        }
    }

   private void NavigateToView(string viewName)
   {
       try
       {
           UserControl? view = viewName switch
           {
               "HomeView" => _serviceProvider.GetService<Views.HomeView>(),
               "UploadView" => _serviceProvider.GetService<Views.UploadView>(),
               "SettingsView" => _serviceProvider.GetService<Views.SettingsView>(),
               "AboutView" => _serviceProvider.GetService<Views.AboutView>(),
               _ => null
           };

           if (view != null)
           {
               CurrentView = view;
               Logger.Info($"Navigated to {viewName}");
           }
           else
           {
               Logger.Warn($"View not found: {viewName}");
           }
       }
       catch (Exception ex)
       {
           Logger.Error(ex, $"Failed to navigate to {viewName}");
       }
   }

    private async Task LogoutAsync()
    {
        try
        {
            await _authService.LogoutAsync();
            Logger.Info("User logged out");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Logout failed");
        }
    }
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool> _canExecute;

    public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;

    public void Execute(object parameter) => _execute((T)parameter);
}