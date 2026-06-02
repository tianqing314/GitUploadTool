using System.Windows;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GitUploadTool.Services;
using GitUploadTool.ViewModels;
using GitUploadTool.Views;
using GitUploadTool.Views.Dialogs;
using MaterialDesignThemes.Wpf;
using NLog;

namespace GitUploadTool;

public partial class App : Application
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private ServiceProvider _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Set dark theme
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetDarkTheme();
        paletteHelper.SetTheme(theme);

        // Check authentication and show login if needed
        var authService = _serviceProvider.GetRequiredService<IAuthenticationService>();
        var isAuthenticated = await authService.IsAuthenticatedAsync();

        if (!isAuthenticated)
        {
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            var result = loginWindow.ShowDialog();

            if (result != true)
            {
                Shutdown();
                return;
            }
        }

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        Logger.Info("Application started");
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        // HttpClient
        services.AddSingleton<HttpClient>();

        // Services
        services.AddTransient<ITokenService, TokenService>();
        services.AddTransient<IAuthenticationService, AuthenticationService>();
        services.AddTransient<IGitHubService, GitHubService>();
        services.AddTransient<IGitService, GitService>();
        services.AddTransient<IRecentProjectService, RecentProjectService>();
        services.AddTransient<ISettingsService, SettingsService>();
        services.AddTransient<IGitIgnoreService, GitIgnoreService>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<UploadViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<CreateRepoDialogViewModel>();

        // Views
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<HomeView>();
        services.AddTransient<UploadView>();
        services.AddTransient<SettingsView>();
        services.AddTransient<AboutView>();
        services.AddTransient<CreateRepoDialog>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}