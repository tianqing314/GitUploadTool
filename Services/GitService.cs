using System.Diagnostics;
using System.IO;
using GitUploadTool.Models;
using NLog;

namespace GitUploadTool.Services;

public class GitService : IGitService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public async Task<bool> IsGitInstalledAsync()
    {
        try
        {
            var result = await RunGitCommandAsync("", "--version");
            return result.Success;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsGitRepositoryAsync(string path)
    {
        var result = await RunGitCommandAsync(path, "rev-parse --is-inside-work-tree");
        return result.Success && result.Output.Trim() == "true";
    }

    public async Task<bool> InitRepositoryAsync(string path)
    {
        var result = await RunGitCommandAsync(path, "init");
        if (result.Success)
        {
            Logger.Info($"Git repository initialized at {path}");
            Logger.Info($"Git init output: {result.Output}");
            return true;
        }
        Logger.Error($"Failed to init repository: {result.Error}");
        Logger.Error($"Git init output: {result.Output}");
        return false;
    }

    public async Task<bool> AddFilesAsync(string path, IProgress<UploadProgress>? progress = null)
    {
        Logger.Info($"Starting git add in: {path}");

        // 先检查大文件，避免 git add 卡在超大文件上
        try
        {
            const long limit = 100L * 1024 * 1024; // 100MB
            var largeFiles = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Where(f => new FileInfo(f).Length > limit)
                .ToList();

            if (largeFiles.Count > 0)
            {
                var names = string.Join("、", largeFiles.Select(f => Path.GetFileName(f)));
                var msg = $"发现 {largeFiles.Count} 个超过 100MB 的文件：{names}。已中止暂存，请移除这些文件或启用 Git LFS 后重试。";
                Logger.Error(msg);
                progress?.Report(new UploadProgress
                {
                    Step = UploadStep.Add,
                    Status = StepStatus.Failed,
                    Message = "检测到大文件",
                    ErrorMessage = msg,
                });
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Large file scan failed before add; continuing");
        }

        var result = await RunGitCommandAsync(path, "add .", timeoutSeconds: 600);
        if (result.Success)
        {
            Logger.Info("Files staged successfully");
            return true;
        }
        Logger.Error($"Failed to add files: {result.Error}");
        Logger.Error($"Git output: {result.Output}");

        progress?.Report(new UploadProgress
        {
            Step = UploadStep.Add,
            Status = StepStatus.Failed,
            Message = "暂存文件失败",
            ErrorMessage = string.IsNullOrEmpty(result.Error) ? result.Output : result.Error,
        });
        return false;
    }

    public async Task<bool> CommitAsync(string path, string message)
    {
        // Ensure git user config exists
        await EnsureGitConfigAsyncAsync(path);

        // 空消息使用默认值，避免 "Aborting commit due to empty commit message"
        if (string.IsNullOrWhiteSpace(message))
        {
            message = $"Upload from GitUploadTool {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            Logger.Info("Commit message was empty, using default");
        }

        var escapedMessage = message.Replace("\"", "\\\"");
        var result = await RunGitCommandAsync(path, $"commit -m \"{escapedMessage}\"");
        if (result.Success)
        {
            Logger.Info($"Committed: {message}");
            return true;
        }
        Logger.Error($"Failed to commit: {result.Error}");
        Logger.Error($"Git output: {result.Output}");

        // 如果提示没有变更（Nothing to commit），视为成功
        if (result.Output.Contains("nothing to commit") || (result.Error?.Contains("nothing to commit") ?? false))
        {
            Logger.Info("Nothing to commit, treating as success");
            return true;
        }

        return false;
    }

    private async Task EnsureGitConfigAsyncAsync(string path)
    {
        // Check if user.name is set
        var nameResult = await RunGitCommandAsync(path, "config user.name");
        if (string.IsNullOrEmpty(nameResult.Output.Trim()))
        {
            await RunGitCommandAsync(path, "config user.name \"GitUploadTool User\"");
            Logger.Info("已设置默认 git user.name");
        }

        // Check if user.email is set
        var emailResult = await RunGitCommandAsync(path, "config user.email");
        if (string.IsNullOrEmpty(emailResult.Output.Trim()))
        {
            await RunGitCommandAsync(path, "config user.email \"gituploadtool@users.noreply.github.com\"");
            Logger.Info("已设置默认 git user.email");
        }
    }

    public async Task<bool> AddRemoteAsync(string path, string remoteUrl, string? token = null)
    {
        // Remove existing remote if any
        await RunGitCommandAsync(path, "remote remove origin");
        
        // Embed token in URL and append .git suffix for git compatibility
        var authenticatedUrl = remoteUrl;
        if (!authenticatedUrl.EndsWith(".git"))
        {
            authenticatedUrl += ".git";
        }
        if (!string.IsNullOrEmpty(token))
        {
            authenticatedUrl = authenticatedUrl.Replace("https://", $"https://x-access-token:{token}@");
        }
        
        var result = await RunGitCommandAsync(path, $"remote add origin {authenticatedUrl}");
        if (result.Success)
        {
            Logger.Info($"Remote added: {remoteUrl}");
            return true;
        }
        Logger.Error($"Failed to add remote: {result.Error}");
        return false;
    }

    public async Task<(bool Success, string Error)> PushAsync(string path, string branch = "main", string? token = null)
    {
        // Authentication is handled via token embedded in the remote URL (added by AddRemoteAsync)
        // Use --force to overwrite remote content (e.g. auto-generated README, .gitignore, LICENSE)
        var pushCommand = $"push -u --force origin {branch}";
        
        var result = await RunGitCommandAsync(path, pushCommand, timeoutSeconds: 300);
        if (result.Success)
        {
            Logger.Info($"Pushed to origin/{branch}");
            return (true, string.Empty);
        }
        var errorMsg = string.IsNullOrEmpty(result.Error) ? result.Output : result.Error;
        Logger.Error($"Failed to push: {errorMsg}");
        return (false, errorMsg);
    }

    public async Task<bool> HasRemoteAsync(string path)
    {
        var result = await RunGitCommandAsync(path, "remote -v");
        return result.Success && result.Output.Contains("origin");
    }

    public async Task<string?> GetRemoteUrlAsync(string path)
    {
        var result = await RunGitCommandAsync(path, "remote get-url origin");
        return result.Success ? result.Output.Trim() : null;
    }

    public async Task<List<UploadProgress>> UploadProjectAsync(
        string path, string remoteUrl, string branch, string commitMessage,
        string? token = null, IProgress<UploadProgress>? progress = null)
    {
        var steps = new List<UploadProgress>();

        // Step 1: Init
        var initProgress = new UploadProgress { Step = UploadStep.Init, Status = StepStatus.Running, Message = "正在初始化 Git 仓库..." };
        progress?.Report(initProgress);
        steps.Add(initProgress);

        if (!await IsGitRepositoryAsync(path))
        {
            if (!await InitRepositoryAsync(path))
            {
                initProgress.Status = StepStatus.Failed;
                initProgress.ErrorMessage = "初始化 Git 仓库失败";
                progress?.Report(initProgress);
                return steps;
            }
        }
        initProgress.Status = StepStatus.Success;
        initProgress.Message = "Git 仓库已就绪";
        progress?.Report(initProgress);

        // Check for files exceeding GitHub's 100 MB limit before staging
        try
        {
            const long githubFileSizeLimit = 100L * 1024 * 1024; // 100 MB
            var largeFiles = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Where(f => new FileInfo(f).Length > githubFileSizeLimit)
                .Select(f => new { Path = f, Size = new FileInfo(f).Length })
                .ToList();

            if (largeFiles.Count > 0)
            {
                var fileList = string.Join("\n", largeFiles.Select(f => $"  • {Path.GetRelativePath(path, f.Path)} ({f.Size / (1024.0 * 1024.0):F2} MB)"));
                var errorMsg = $"发现 {largeFiles.Count} 个文件超过 GitHub 的 100MB 限制：\n{fileList}\n\n" +
                               "请考虑使用 Git LFS (https://git-lfs.github.com) 或在上传前移除这些文件。";
                var largeFileProgress = new UploadProgress
                {
                    Step = UploadStep.Init,
                    Status = StepStatus.Failed,
                    Message = "检测到大文件",
                    ErrorMessage = errorMsg
                };
                progress?.Report(largeFileProgress);
                steps.Add(largeFileProgress);
                return steps;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to scan for large files; proceeding anyway");
        }

        // Step 2: Add
        var addProgress = new UploadProgress { Step = UploadStep.Add, Status = StepStatus.Running, Message = "正在暂存文件..." };
        progress?.Report(addProgress);
        steps.Add(addProgress);

        if (!await AddFilesAsync(path, progress))
        {
            addProgress.Status = StepStatus.Failed;
            addProgress.ErrorMessage = "暂存文件失败";
            progress?.Report(addProgress);
            return steps;
        }
        addProgress.Status = StepStatus.Success;
        addProgress.Message = "文件已暂存";
        progress?.Report(addProgress);

        // Step 3: Commit
        var commitProgress = new UploadProgress { Step = UploadStep.Commit, Status = StepStatus.Running, Message = "正在提交更改..." };
        progress?.Report(commitProgress);
        steps.Add(commitProgress);

        if (!await CommitAsync(path, commitMessage))
        {
            commitProgress.Status = StepStatus.Failed;
            commitProgress.ErrorMessage = "提交更改失败";
            progress?.Report(commitProgress);
            return steps;
        }
        commitProgress.Status = StepStatus.Success;
        commitProgress.Message = "更改已提交";
        progress?.Report(commitProgress);

        // Ensure local branch name matches the target branch
        await RunGitCommandAsync(path, $"branch -M {branch}");

        // Step 4: Remote & Push
        var pushProgress = new UploadProgress { Step = UploadStep.Push, Status = StepStatus.Running, Message = "正在添加远程仓库并推送..." };
        progress?.Report(pushProgress);
        steps.Add(pushProgress);

        if (!await AddRemoteAsync(path, remoteUrl, token))
        {
            pushProgress.Status = StepStatus.Failed;
            pushProgress.ErrorMessage = "添加远程仓库失败";
            progress?.Report(pushProgress);
            return steps;
        }

        var pushResult = await PushAsync(path, branch, token);
        if (!pushResult.Success)
        {
            pushProgress.Status = StepStatus.Failed;
            pushProgress.ErrorMessage = string.IsNullOrEmpty(pushResult.Error)
                ? "推送到远程仓库失败"
                : $"推送失败：{pushResult.Error}";
            progress?.Report(pushProgress);
            return steps;
        }
        pushProgress.Status = StepStatus.Success;
        pushProgress.Message = "已推送到远程仓库";
        progress?.Report(pushProgress);

        // Complete
        var completeProgress = new UploadProgress { Step = UploadStep.Complete, Status = StepStatus.Success, Message = "上传完成！" };
        progress?.Report(completeProgress);
        steps.Add(completeProgress);

        return steps;
    }

    private async Task<(bool Success, string Output, string Error)> RunGitCommandAsync(string workingDir, string arguments, int timeoutSeconds = 60)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return (false, "", "启动 git 进程失败");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            
            try
            {
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync(cts.Token);
                
                var output = await outputTask;
                var error = await errorTask;

                return (process.ExitCode == 0, output, error);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                return (false, "", $"命令在 {timeoutSeconds} 秒后超时");
            }
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }

    public async Task<bool> EnsureGitLfsInstalledAsync(string path)
    {
        try
        {
            // 先检查 git lfs version 是否可用
            var check = await RunGitCommandAsync(path, "lfs version", timeoutSeconds: 15);
            if (check.Success)
            {
                // 确保仓库已初始化 LFS（幂等操作）
                await RunGitQuiet(path, "lfs install");
                return true;
            }
            Logger.Error("git-lfs 未安装，请从 https://git-lfs.github.com 安装后重试");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "EnsureGitLfsInstalled failed");
            return false;
        }
    }

    public async Task<bool> TrackLargeFilesWithLfsAsync(string path, List<FileInfo> files)
    {
        try
        {
            if (files.Count == 0) return true;

            var tracked = 0;
            foreach (var file in files)
            {
                // 计算相对路径并统一为 / 分隔符
                var relPath = file.FullName.Substring(path.Length + 1).Replace('\\', '/');
                var result = await RunGitCommandAsync(path, $"lfs track \"{relPath}\"", timeoutSeconds: 30);
                if (result.Success)
                    tracked++;
                else
                    Logger.Warn($"LFS track failed for {relPath}: {result.Error}");
            }

            Logger.Info($"LFS tracked {tracked}/{files.Count} files");
            return tracked == files.Count;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "TrackLargeFilesWithLfs failed");
            return false;
        }
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
}
