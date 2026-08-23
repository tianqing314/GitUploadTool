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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded 可能触发多次（窗口最大化/最小化等），只初始化一次
        if (_initialized || _webView != null) return;
        _initialized = true;

        try
        {
            // 创建 WebView2 并添加到 Grid 中
            var webView = new WebView2();
            var grid = this.FindName("RootGrid") as Grid;
            if (grid != null)
            {
                grid.Children.Add(webView);
                webView.SetValue(Grid.RowSpanProperty, 2); // 占满整个 Grid
            }
            _webView = webView;

            // 等待 CoreWebView2 初始化完成
            webView.EnsureCoreWebView2Async().ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    NLog.LogManager.GetCurrentClassLogger().Error(t.Exception, "EnsureCoreWebView2Async failed");
                    return;
                }

                if (webView.CoreWebView2 == null)
                {
                    NLog.LogManager.GetCurrentClassLogger().Error("CoreWebView2 is null after initialization");
                    return;
                }

                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                // 先通知宿主绑定桥接，再加载页面
                CoreInitialized?.Invoke(webView);

                var indexPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
                if (File.Exists(indexPath))
                {
                    webView.CoreWebView2.Navigate("file:///" + indexPath.Replace('\\', '/'));
                    NLog.LogManager.GetCurrentClassLogger().Info("Frontend loaded: {Path}", indexPath);
                }
                else
                {
                    NLog.LogManager.GetCurrentClassLogger().Error("Frontend not found: {Path}", indexPath);
                    webView.CoreWebView2.NavigateToString("<html><body><h1>Error</h1><p>Frontend files not found.</p></body></html>");
                }
            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        }
        catch (Exception ex)
        {
            _initialized = false; // 允许下次重试
            NLog.LogManager.GetCurrentClassLogger().Error(ex, "WebView2 init failed");
        }
    }

    /// <summary>
    /// 获取内部 WebView2 实例（供桥接类使用）
    /// </summary>
    public WebView2? GetInnerWebView() => _webView;
}
