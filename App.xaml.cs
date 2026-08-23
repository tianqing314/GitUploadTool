using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GitUploadTool.Services;
using GitUploadTool.Bridge;

namespace GitUploadTool;

/// <summary>
/// App.xaml 的交互逻辑
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 确保工作目录是 exe 所在目录，wwwroot 等相对路径可正常解析
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        Directory.SetCurrentDirectory(exeDir);

        // 配置依赖注入
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // 直接启动主窗口；登录由前端页处理
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // 配置 appsettings
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        // 注册 HttpClient
        services.AddSingleton<HttpClient>();

        // 注册核心 Services
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IGitHubService, GitHubService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IRecentProjectService, RecentProjectService>();
        services.AddSingleton<IGitIgnoreService, GitIgnoreService>();

        // 注册 WebView2 桥接
        services.AddSingleton<WebViewBridge>();

        // 注册主窗口
        services.AddTransient<MainWindow>();
    }
}
