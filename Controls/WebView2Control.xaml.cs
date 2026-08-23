using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace GitUploadTool.Controls;

/// <summary>
/// 自定义 WebView2 控件，用于承载前端页面
/// </summary>
public partial class WebView2Control : UserControl
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private WebView2? _webView;
    private bool _initialized;

    /// <summary>
    /// WebView2 核心初始化完成后触发（EnsureCoreWebView2Async 完成时）
    /// </summary>
    public event Action<WebView2>? CoreInitialized;

    public WebView2Control()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded 可能触发多次（窗口最大化/最小化等），只初始化一次
        if (_initialized || _webView != null) return;
        _initialized = true;

        try
        {
            // 创建 WebView2 并添加到 Grid 中
            var webView = new WebView2();
            var grid = this.FindName("RootGrid") as Grid;
            grid?.Children.Add(webView);
            _webView = webView;

            // 必须用 await（而非 ContinueWith）确保在 UI 线程上正确完成初始化
            await webView.EnsureCoreWebView2Async();

            if (webView.CoreWebView2 == null)
            {
                Logger.Error("CoreWebView2 is null after initialization");
                _initialized = false;
                return;
            }

            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            // 先通知宿主绑定桥接，再加载页面
            CoreInitialized?.Invoke(webView);

            var indexPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
            if (File.Exists(indexPath))
            {
                // 用 Uri 对象确保路径转义正确（中文/空格等）
                webView.Source = new Uri(indexPath);
                Logger.Info("Frontend loaded: {Path}", indexPath);
            }
            else
            {
                Logger.Error("Frontend not found: {Path}", indexPath);
                webView.CoreWebView2.NavigateToString("<html><body><h1>Error</h1><p>Frontend files not found.</p></body></html>");
            }
        }
        catch (Exception ex)
        {
            _initialized = false; // 允许下次重试
            Logger.Error(ex, "WebView2 init failed");
        }
    }

    /// <summary>
    /// 获取内部 WebView2 实例（供桥接类使用）
    /// </summary>
    public WebView2? GetInnerWebView() => _webView;
}