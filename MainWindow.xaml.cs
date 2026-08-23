using System;
using System.Windows;
using GitUploadTool.Bridge;
using Microsoft.Extensions.DependencyInjection;

namespace GitUploadTool;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private bool _isDragging = false;

    public MainWindow(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var webViewControl = FindName("WebViewControl") as Controls.WebView2Control;
        if (webViewControl == null)
        {
            NLog.LogManager.GetCurrentClassLogger().Error("WebViewControl not found");
            return;
        }

        // 等 WebView2 核心初始化完成后再绑定桥接（CoreWebView2 就绪后才会触发）
        webViewControl.CoreInitialized += (webView) =>
        {
            try
            {
                var bridge = _serviceProvider.GetRequiredService<WebViewBridge>();
                bridge.SetWebView(webView);
                NLog.LogManager.GetCurrentClassLogger().Info("WebView2 bridge initialized");
            }
            catch (Exception ex)
            {
                NLog.LogManager.GetCurrentClassLogger().Error(ex, "Failed to initialize WebView2 bridge");
            }
        };
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }
}