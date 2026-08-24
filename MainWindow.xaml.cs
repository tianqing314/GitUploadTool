using System;
using System.Windows;
using System.Windows.Input;
using GitUploadTool.Bridge;
using Microsoft.Extensions.DependencyInjection;

namespace GitUploadTool;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;

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
        ToggleMaximize();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // 双击标题栏切换最大化/还原
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        // 最大化状态下不允许拖动，直接返回
        if (WindowState == WindowState.Maximized)
        {
            return;
        }

        DragMove();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}