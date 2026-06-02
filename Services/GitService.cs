using System.Diagnostics;
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

    public async Task<bool> AddFilesAsync(string path)
    {
        Logger.Info($"Starting git add in: {path}");
        var result = await RunGitCommandAsync(path, "add .", timeoutSeconds: 300);
        if (result.Success)
        {
            Logger.Info("Files staged successfully");
            return true;
        }
        Logger.Error($"Failed to add files: {result.Error}");
        Logger.Error($"Git output: {result.Output}");
        return false;
    }

    public async Task<bool> CommitAsync(string path, string message)
    {
        // Ensure git user config exists
        await EnsureGitConfigAsync(path);
        
        var escapedMessage = message.Replace("\"", "\\\"");
        var result = await RunGitCommandAsync(path, $"commit -m \"{escapedMessage}\"");
        if (result.Success)
        {
            Logger.Info($"Committed: {message}");
            return true;
        }
        Logger.Error($"Failed to commit: {result.Error}");
        Logger.Error($"Git output: {result.Output}");
        return false;
    }

    private async Task EnsureGitConfigAsync(string path)
    {
        // Check if user.name is set
        var nameResult = await RunGitCommandAsync(path, "config user.name");
        if (string.IsNullOrEmpty(nameResult.Output.Trim()))
        {
            await RunGitCommandAsync(path, "config user.name \"GitUploadTool User\"");
            Logger.Info("Set default git user.name");
        }

        // Check if user.email is set
        var emailResult = await RunGitCommandAsync(path, "config user.email");
        if (string.IsNullOrEmpty(emailResult.Output.Trim()))
        {
            await RunGitCommandAsync(path, "config user.email \"gituploadtool@users.noreply.github.com\"");
            Logger.Info("Set default git user.email");
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
        var pushCommand = $"push -u origin {branch}";
        
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
        var initProgress = new UploadProgress { Step = UploadStep.Init, Status = StepStatus.Running, Message = "Initializing git repository..." };
        progress?.Report(initProgress);
        steps.Add(initProgress);

        if (!await IsGitRepositoryAsync(path))
        {
            if (!await InitRepositoryAsync(path))
            {
                initProgress.Status = StepStatus.Failed;
                initProgress.ErrorMessage = "Failed to initialize git repository";
                progress?.Report(initProgress);
                return steps;
            }
        }
        initProgress.Status = StepStatus.Success;
        initProgress.Message = "Git repository ready";
        progress?.Report(initProgress);

        // Step 2: Add
        var addProgress = new UploadProgress { Step = UploadStep.Add, Status = StepStatus.Running, Message = "Staging files..." };
        progress?.Report(addProgress);
        steps.Add(addProgress);

        if (!await AddFilesAsync(path))
        {
            addProgress.Status = StepStatus.Failed;
            addProgress.ErrorMessage = "Failed to stage files";
            progress?.Report(addProgress);
            return steps;
        }
        addProgress.Status = StepStatus.Success;
        addProgress.Message = "Files staged";
        progress?.Report(addProgress);

        // Step 3: Commit
        var commitProgress = new UploadProgress { Step = UploadStep.Commit, Status = StepStatus.Running, Message = "Committing changes..." };
        progress?.Report(commitProgress);
        steps.Add(commitProgress);

        if (!await CommitAsync(path, commitMessage))
        {
            commitProgress.Status = StepStatus.Failed;
            commitProgress.ErrorMessage = "Failed to commit changes";
            progress?.Report(commitProgress);
            return steps;
        }
        commitProgress.Status = StepStatus.Success;
        commitProgress.Message = "Changes committed";
        progress?.Report(commitProgress);

        // Ensure local branch name matches the target branch
        await RunGitCommandAsync(path, $"branch -M {branch}");

        // Step 4: Remote & Push
        var pushProgress = new UploadProgress { Step = UploadStep.Push, Status = StepStatus.Running, Message = "Adding remote and pushing..." };
        progress?.Report(pushProgress);
        steps.Add(pushProgress);

        if (!await AddRemoteAsync(path, remoteUrl, token))
        {
            pushProgress.Status = StepStatus.Failed;
            pushProgress.ErrorMessage = "Failed to add remote";
            progress?.Report(pushProgress);
            return steps;
        }

        var pushResult = await PushAsync(path, branch, token);
        if (!pushResult.Success)
        {
            pushProgress.Status = StepStatus.Failed;
            pushProgress.ErrorMessage = string.IsNullOrEmpty(pushResult.Error)
                ? "Failed to push to remote"
                : $"Push failed: {pushResult.Error}";
            progress?.Report(pushProgress);
            return steps;
        }
        pushProgress.Status = StepStatus.Success;
        pushProgress.Message = "Pushed to remote";
        progress?.Report(pushProgress);

        // Complete
        var completeProgress = new UploadProgress { Step = UploadStep.Complete, Status = StepStatus.Success, Message = "Upload complete!" };
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
                return (false, "", "Failed to start git process");

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
                return (false, "", $"Command timed out after {timeoutSeconds} seconds");
            }
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }
}
