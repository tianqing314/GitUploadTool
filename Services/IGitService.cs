using GitUploadTool.Models;

namespace GitUploadTool.Services;

public interface IGitService
{
    Task<bool> IsGitInstalledAsync();
    Task<bool> IsGitRepositoryAsync(string path);
    Task<bool> InitRepositoryAsync(string path);
    Task<bool> AddFilesAsync(string path);
    Task<bool> CommitAsync(string path, string message);
    Task<bool> AddRemoteAsync(string path, string remoteUrl, string? token = null);
    Task<bool> PushAsync(string path, string branch = "main", string? token = null);
    Task<bool> HasRemoteAsync(string path);
    Task<string?> GetRemoteUrlAsync(string path);
    Task<List<UploadProgress>> UploadProjectAsync(string path, string remoteUrl, string branch, string commitMessage, string? token = null, IProgress<UploadProgress>? progress = null);
}
