using System.Net.Http;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using GitUploadTool.Models;
using GitUploadTool.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using NLog;

namespace GitUploadTool.Bridge;

/// <summary>
/// WebView2 与 C# 后端通信桥接类
/// 前端通过 window.chrome.webview.postMessage(JSON字符串) 发送 {action, ...} 消息
/// 后端通过 PostWebMessageAsJson 推送 {evt, data} 消息
/// </summary>
public class WebViewBridge
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IServiceProvider _serviceProvider;
    private Microsoft.Web.WebView2.Wpf.WebView2? _webView;
    private GitHubUser? _currentUser;
    private List<FileInfo> _largeFileCache = new();
    private List<string> _excludedLargeFiles = new();
    private string _selectedPath = string.Empty;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public WebViewBridge(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void SetWebView(Microsoft.Web.WebView2.Wpf.WebView2 webView)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            // JS postMessage(JSON.stringify(obj)) 时 WebMessageAsJson 是双重编码，
            // 需要先解一层得到内层 JSON 字符串再解析
            var outer = e.WebMessageAsJson ?? string.Empty;
            if (string.IsNullOrWhiteSpace(outer))
                return;

            using var doc = JsonDocument.Parse(outer);
            var root = doc.RootElement.Clone();

            // 如果是字符串类型说明双重编码，再解一层
            if (root.ValueKind == JsonValueKind.String)
            {
                var inner = root.GetString() ?? string.Empty;
                using var innerDoc = JsonDocument.Parse(inner);
                HandlePayload(innerDoc.RootElement.Clone());
            }
            else
            {
                HandlePayload(root);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to handle web message");
            Send("error", new { message = ex.Message });
        }
    }

    private void HandlePayload(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("action", out var actionProp))
            return;

        var action = actionProp.GetString();
        Logger.Debug("Received action: {Action}", action);

        switch (action)
        {
            case "check_auth":
                _ = Task.Run(async () =>
                {
                    var r = await CheckAuthAsync();
                    Send("authStatus", new { isAuthenticated = r.authenticated, user = r.user });
                    if (r.authenticated)
                        Send("loginSuccess", new { user = r.user });
                });
                break;

            case "login":
            case "oauth_login":
                _ = Task.Run(async () =>
                {
                    var ok = await OAuthLoginAsync();
                    if (ok)
                        Send("loginSuccess", new { user = _currentUser });
                    else
                        Send("loginFailed", new { message = "OAuth 登录失败" });
                });
                break;

            case "token_login":
            case "tokenLogin":
                _ = Task.Run(async () => await TokenLoginAsync(root));
                break;

            case "logout":
                _ = Task.Run(async () =>
                {
                    await LogoutAsync();
                    Send("logoutSuccess", null);
                });
                break;

            case "userInfo":
            case "get_current_user":
                _ = Task.Run(async () =>
                {
                    await EnsureUserAsync();
                    Send("userInfo", _currentUser);
                });
                break;

            case "repositories":
            case "get_repositories":
                _ = Task.Run(async () => await GetRepositoriesAsync());
                break;

            case "select_folder":
                SelectFolderDialog();
                break;

            case "scanProject":
            case "scan_files":
            {
                var path = root.TryGetProperty("path", out var pProp) ? pProp.GetString() : _selectedPath;
                _selectedPath = path ?? string.Empty;
                _ = Task.Run(async () => await ScanProjectAsync(_selectedPath));
                break;
            }

            case "startUpload":
            case "upload_project":
            {
                var path = root.TryGetProperty("path", out var pp) ? pp.GetString() : _selectedPath;
                var rcRaw = root.TryGetProperty("repoConfig", out var rc) ? rc.GetRawText() : "{}";
                _ = Task.Run(async () => await UploadProjectAsync(path ?? _selectedPath, rcRaw));
                break;
            }

            case "enableLFS":
            case "excludeLargeFiles":
            {
                // 前端把勾选排除的大文件列表一并传过来
                var files = new List<string>();
                if (root.TryGetProperty("files", out var fProp) && fProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in fProp.EnumerateArray())
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrEmpty(s)) files.Add(s);
                    }
                }
                _excludedLargeFiles = files;
                Send("largeFileAction", new { action, success = true, count = files.Count });
                break;
            }

            case "excludeFiles":
            {
                // 独立排除文件 action：立即写入 .gitignore 并从缓存移除
                var files = new List<string>();
                if (root.TryGetProperty("files", out var efProp) && efProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in efProp.EnumerateArray())
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrEmpty(s)) files.Add(s);
                    }
                }
                _excludedLargeFiles = files;
                if (!string.IsNullOrEmpty(_selectedPath) && files.Count > 0)
                {
                    _ = Task.Run(async () => await AppendToGitIgnoreAsync(_selectedPath, files));
                }
                Send("largeFileAction", new { action = "excludeFiles", success = true, count = files.Count });
                break;
            }

            case "loadHistory":
                _ = Task.Run(async () => await LoadHistoryAsync());
                break;

            case "loadSettings":
            case "get_settings":
                _ = Task.Run(async () => await GetSettingsAsync());
                break;

            case "saveSettings":
            case "save_settings":
            {
                if (root.TryGetProperty("settings", out var sProp))
                {
                    var raw = sProp.GetRawText();
                    _ = Task.Run(async () => await SaveSettingsAsync(raw));
                }
                break;
            }

            case "openUrl":
            case "open_url":
            {
                var url = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : string.Empty;
                OpenUrl(url ?? string.Empty);
                break;
            }

            default:
                Logger.Warn("Unknown action from frontend: {Action}", action);
                break;
        }
    }

    private void Send(string evt, object? data)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { evt, data }, _jsonOptions);
            _webView?.Dispatcher.Invoke(() =>
            {
                _webView?.CoreWebView2?.PostWebMessageAsJson(payload);
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to send message to webview");
        }
    }

    // ============ Auth ============
    private async Task<(bool authenticated, GitHubUser? user)> CheckAuthAsync()
    {
        try
        {
            var auth = _serviceProvider.GetRequiredService<IAuthenticationService>();
            if (!await auth.IsAuthenticatedAsync())
                return (false, null);

            _currentUser = await auth.GetCurrentUserAsync();
            return (_currentUser != null, _currentUser);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "CheckAuth failed");
            return (false, null);
        }
    }

    private async Task<bool> OAuthLoginAsync()
    {
        try
        {
            var auth = _serviceProvider.GetRequiredService<IAuthenticationService>();
            var ok = await auth.LoginAsync();
            if (ok)
                _currentUser = await auth.GetCurrentUserAsync();
            return ok && _currentUser != null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "OAuthLogin failed");
            return false;
        }
    }

    private async Task TokenLoginAsync(JsonElement root)
    {
        try
        {
            var token = root.TryGetProperty("token", out var tProp) ? tProp.GetString() : string.Empty;

            if (string.IsNullOrWhiteSpace(token))
            {
                Send("tokenLoginFailed", new { message = "请输入 Token" });
                return;
            }

            // 读取前端传来的平台信息
            var platform = root.TryGetProperty("platform", out var pProp) ? pProp.GetString() : "github";
            var gitlabUrl = root.TryGetProperty("gitlabUrl", out var gProp) ? gProp.GetString() : string.Empty;

            // 保存 token 并用 /user 接口验证有效性
            var tokenService = _serviceProvider.GetRequiredService<ITokenService>();
            await tokenService.SaveTokenAsync(token);

            // 根据平台获取用户信息
            var platformService = ResolvePlatformService(platform, gitlabUrl);
            _currentUser = await platformService.GetUserAsync();

            if (_currentUser == null)
            {
                // token 无效，回滚删除
                await tokenService.DeleteTokenAsync();
                Send("tokenLoginFailed", new { message = "Token 无效或已过期" });
                return;
            }

            // 保存平台偏好
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            await settingsService.SaveSettingsAsync(new AppSettings
            {
                Platform = platform,
                GitLabInstanceUrl = string.IsNullOrEmpty(gitlabUrl) ? null : gitlabUrl,
            });

            Logger.Info("Token login success: {Login} on {Platform}", _currentUser.Login, platform);
            Send("tokenLoginSuccess", new { user = _currentUser });
            Send("loginSuccess", new { user = _currentUser });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "TokenLogin failed");
            Send("tokenLoginFailed", new { message = ex.Message });
        }
    }

    private async Task LogoutAsync()
    {
        var auth = _serviceProvider.GetRequiredService<IAuthenticationService>();
        await auth.LogoutAsync();
        _currentUser = null;
    }

    private async Task EnsureUserAsync()
    {
        if (_currentUser == null)
        {
            var auth = _serviceProvider.GetRequiredService<IAuthenticationService>();
            if (await auth.IsAuthenticatedAsync())
                _currentUser = await auth.GetCurrentUserAsync();
        }
    }

    // ============ Repositories ============
    private async Task GetRepositoriesAsync()
    {
        try
        {
            var settings = await _serviceProvider.GetRequiredService<ISettingsService>().GetSettingsAsync();
            var platform = ResolvePlatformService(settings.Platform, settings.GitLabInstanceUrl);
            var repos = await platform.GetPlatformRepositoriesAsync();
            Send("repositories", new { success = true, items = repos });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetRepositories failed");
            Send("repositories", new { success = false, error = ex.Message, items = Array.Empty<object>() });
        }
    }

    // ============ Folder & Files ============
    private void SelectFolderDialog()
    {
        try
        {
            var path = string.Empty;
            _webView?.Dispatcher.Invoke(() =>
            {
                var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    Title = "选择项目文件夹"
                };
                if (dialog.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
                    path = dialog.FileName ?? string.Empty;
            });

            if (!string.IsNullOrEmpty(path))
            {
                _selectedPath = path;
                var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                Send("projectSelected", new { path, name });
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "SelectFolder failed");
        }
    }

    private async Task ScanProjectAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            Send("scanResult", new { success = false, error = "目录不存在" });
            return;
        }

        try
        {
            long totalSize = 0;
            int fileCount = 0;
            var largeFiles = new List<object>();
            _largeFileCache.Clear();

            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var fi = new FileInfo(file);
                    totalSize += fi.Length;
                    fileCount++;

                    if (fi.Length > 50L * 1024 * 1024) // >50MB
                    {
                        _largeFileCache.Add(fi);
                        largeFiles.Add(new
                        {
                            name = Path.GetFileName(file),
                            path,
                            size = fi.Length,
                        });
                    }
                }
                catch { /* skip inaccessible files */ }
            }

            var hasOver100MB = largeFiles.Any(f => ((long)f.GetType().GetProperty("size")!.GetValue(f)!) > 100L * 1024 * 1024);
            Send("scanResult", new
            {
                success = true,
                totalFiles = fileCount,
                totalSize,
                largeFiles,
                hasOver100MB,
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ScanProject failed");
            Send("scanResult", new { success = false, error = ex.Message });
        }
    }

    // ============ Upload ============
    private async Task UploadProjectAsync(string path, string repoConfigRaw)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                Send("uploadResult", new { success = false, error = "项目目录无效" });
                return;
            }

            RepoConfig cfg;
            try
            {
                cfg = JsonSerializer.Deserialize<RepoConfig>(repoConfigRaw, _jsonOptions) ?? new RepoConfig();
            }
            catch
            {
                cfg = new RepoConfig();
            }

            await EnsureUserAsync();
            if (_currentUser == null)
            {
                Send("uploadResult", new { success = false, error = "未登录或登录已过期" });
                return;
            }

            var repoName = string.IsNullOrWhiteSpace(cfg.name)
                ? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                : cfg.name;
            var branch = string.IsNullOrWhiteSpace(cfg.branch) ? "main" : cfg.branch;

            Send("uploadProgress", new { status = "正在检查仓库...", subStatus = repoName, progress = 5 });

            // 根据保存的设置解析平台
            var settings = await _serviceProvider.GetRequiredService<ISettingsService>().GetSettingsAsync();
            var platform = ResolvePlatformService(settings.Platform, settings.GitLabInstanceUrl);

            // 检查/创建仓库（平台无关）
            var repos = await platform.GetPlatformRepositoriesAsync();
            var existing = repos.FirstOrDefault(r => r.Name.Equals(repoName, StringComparison.OrdinalIgnoreCase));
            RepositoryInfo? repo;
            if (existing != null)
            {
                repo = existing;
                Send("uploadProgress", new { status = "仓库已存在", subStatus = existing.FullName, progress = 15 });
            }
            else
            {
                Send("uploadProgress", new { status = "正在创建仓库...", subStatus = repoName, progress = 10 });
                repo = await platform.CreatePlatformRepositoryAsync(repoName, cfg.description, cfg.visibility == "private");
                if (repo == null)
                {
                    Send("uploadResult", new { success = false, error = $"在 {platform.PlatformName} 创建仓库 {repoName} 失败" });
                    return;
                }
            }

            // .gitignore 模板
            if (!string.IsNullOrEmpty(cfg.gitignore))
            {
                try
                {
                    var gi = _serviceProvider.GetRequiredService<IGitIgnoreService>();
                    var tpl = gi.GetTemplates().FirstOrDefault(t => t.Language == cfg.gitignore);
                    if (tpl != null)
                    {
                        await gi.ApplyTemplateAsync(path, tpl.Language);
                        Send("uploadProgress", new { status = "已应用 .gitignore 模板", subStatus = tpl.Name, progress = 20 });
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Apply gitignore template failed");
                }
            }

            // 大文件处理：排除方案写入 .gitignore（优先使用前端勾选列表）
            if (cfg.excludeLarge)
            {
                var excludeList = _excludedLargeFiles.Count > 0
                    ? _excludedLargeFiles
                    : _largeFileCache.Select(f => f.FullName.Substring(path.Length + 1).Replace('\\', '/')).ToList();

                if (excludeList.Count > 0)
                {
                    await AppendToGitIgnoreAsync(path, excludeList);
                    Send("uploadProgress", new { status = $"已排除 {excludeList.Count} 个大文件", subStatus = "", progress = 22 });
                }
            }

            // 大文件处理：LFS 方案（精确跟踪勾选的文件；未勾选则按扩展名跟踪全部缓存）
            if (cfg.useLfs && _largeFileCache.Count > 0)
            {
                Send("uploadProgress", new { status = "配置 Git LFS...", subStatus = "", progress = 25 });

                var gitSvcForLfs = _serviceProvider.GetRequiredService<IGitService>();
                var lfsReady = await gitSvcForLfs.EnsureGitLfsInstalledAsync(path);
                if (!lfsReady)
                {
                    Send("uploadResult", new { success = false, error = "Git LFS 未安装或初始化失败，请先安装 git-lfs（https://git-lfs.github.com）" });
                    return;
                }

                // 排除已被用户排除的文件，剩余用 LFS 跟踪
                var lfsTargets = _largeFileCache
                    .Where(f => !_excludedLargeFiles.Contains(f.FullName.Substring(path.Length + 1).Replace('\\', '/')))
                    .ToList();

                if (lfsTargets.Count > 0)
                {
                    await gitSvcForLfs.TrackLargeFilesWithLfsAsync(path, lfsTargets);
                    Send("uploadProgress", new { status = $"已启用 LFS 跟踪 {lfsTargets.Count} 个大文件", subStatus = "", progress = 27 });
                }
            }

            // 执行 git 上传（init/add/commit/push）
            var gitSvc = _serviceProvider.GetRequiredService<IGitService>();
            var tokenSvc = _serviceProvider.GetRequiredService<ITokenService>();
            var token = await tokenSvc.GetTokenAsync();

            // 构建 remote URL：GitHub 用 github.com，GitLab 用实例地址
            var remoteUrl = settings.Platform?.ToLowerInvariant() == "gitlab"
                ? $"{settings.GitLabInstanceUrl}/{_currentUser.Login}/{repoName}"
                : $"https://github.com/{_currentUser.Login}/{repoName}";
            var progress = new Progress<UploadProgress>(p =>
            {
                var pct = p.Step switch
                {
                    UploadStep.Init => 30,
                    UploadStep.Add => 50,
                    UploadStep.Commit => 70,
                    UploadStep.Push => 85,
                    UploadStep.Complete => 98,
                    _ => 40
                };
                Send("uploadProgress", new
                {
                    status = p.Message,
                    subStatus = p.Step.ToString(),
                    progress = pct,
                });
            });

            Send("uploadProgress", new { status = "开始上传...", subStatus = "", progress = 28 });
            var steps = await gitSvc.UploadProjectAsync(path, remoteUrl, branch, cfg.description ?? $"Upload {repoName}", token, progress);

            var failed = steps.LastOrDefault(s => s.Status == StepStatus.Failed);
            if (failed != null)
            {
                Send("uploadResult", new { success = false, error = failed.ErrorMessage ?? "上传失败" });
                return;
            }

            var repoUrl = repo.HtmlUrl ?? remoteUrl;
            Send("uploadProgress", new { status = "上传完成", subStatus = "", progress = 100 });
            Send("uploadSuccess", new { success = true, repoUrl });

            // 保存历史记录
            try
            {
                var recent = _serviceProvider.GetRequiredService<IRecentProjectService>();
                await recent.AddRecentProjectAsync(new RecentProject
                {
                    Name = repoName,
                    Path = path,
                    RepoUrl = repoUrl,
                    UploadTime = DateTime.Now,
                    Branch = branch,
                });
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Save recent project failed");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "UploadProject failed");
            Send("uploadResult", new { success = false, error = ex.Message });
        }
    }

    private class RepoConfig
    {
        public string name { get; set; } = string.Empty;
        public string? description { get; set; }
        public string visibility { get; set; } = "public";
        public string branch { get; set; } = "main";
        public string? gitignore { get; set; }
        public bool useLfs { get; set; }
        public bool excludeLarge { get; set; }
    }

    private async Task<bool> RunGitQuiet(string workDir, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "git command failed: {Args}", args);
            return false;
        }
    }

    /// <summary>
    /// 将文件路径追加到项目 .gitignore（去重，避免重复条目）
    /// </summary>
    private async Task AppendToGitIgnoreAsync(string projectPath, List<string> relativePaths)
    {
        try
        {
            var giPath = Path.Combine(projectPath, ".gitignore");
            var existing = File.Exists(giPath) ? await File.ReadAllLinesAsync(giPath) : Array.Empty<string>();
            var existingSet = new HashSet<string>(existing.Select(l => l.Trim()), StringComparer.OrdinalIgnoreCase);

            var toAppend = new List<string> { "", "# GitUploadTool: excluded files" };
            foreach (var p in relativePaths)
            {
                var normalized = p.Replace('\\', '/').Trim();
                if (!existingSet.Contains(normalized))
                {
                    toAppend.Add(normalized);
                    existingSet.Add(normalized);
                }
            }

            if (toAppend.Count > 2) // 只有分隔注释则无需写入
            {
                await File.AppendAllLinesAsync(giPath, toAppend);
                Logger.Info("Appended {Count} entries to .gitignore", toAppend.Count - 2);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "AppendToGitIgnore failed");
        }
    }

    // ============ History & Settings ============
    private async Task LoadHistoryAsync()
    {
        try
        {
            var recent = _serviceProvider.GetRequiredService<IRecentProjectService>();
            var items = await recent.GetRecentProjectsAsync();
            Send("history", new { success = true, items });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "LoadHistory failed");
            Send("history", new { success = false, items = Array.Empty<object>() });
        }
    }

    private async Task GetSettingsAsync()
    {
        try
        {
            var svc = _serviceProvider.GetRequiredService<ISettingsService>();
            var s = await svc.GetSettingsAsync();
            Send("settings", s);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GetSettings failed");
            Send("settings", new AppSettings());
        }
    }

    private async Task SaveSettingsAsync(string raw)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<AppSettings>(raw, _jsonOptions);
            if (payload == null)
            {
                Send("settingsSaved", new { success = false });
                return;
            }

            var svc = _serviceProvider.GetRequiredService<ISettingsService>();
            await svc.SaveSettingsAsync(payload);
            Send("settingsSaved", new { success = true });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "SaveSettings failed");
            Send("settingsSaved", new { success = false, error = ex.Message });
        }
    }

    private void OpenUrl(string url)
    {
        try
        {
            if (!string.IsNullOrEmpty(url))
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "OpenUrl failed: {Url}", url);
        }
    }

    /// <summary>
    /// 根据平台标识解析对应的 IPlatformService 实例
    /// </summary>
    private IPlatformService ResolvePlatformService(string? platform, string? gitlabUrl)
    {
        var tokenService = _serviceProvider.GetRequiredService<ITokenService>();
        var httpClient = _serviceProvider.GetRequiredService<HttpClient>();

        return (platform?.ToLowerInvariant()) switch
        {
            "gitlab" => new GitLabService(tokenService, httpClient,
                string.IsNullOrWhiteSpace(gitlabUrl) ? "https://gitlab.com" : gitlabUrl),
            _ => _serviceProvider.GetRequiredService<IGitHubService>(), // 默认 GitHub
        };
    }
}
