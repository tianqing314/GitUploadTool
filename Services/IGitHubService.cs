using GitUploadTool.Models;

namespace GitUploadTool.Services;

public interface IGitHubService
{
    Task<GitHubUser?> GetUserAsync();
    Task<List<GitHubRepository>> GetRepositoriesAsync();
    Task<GitHubRepository?> GetRepositoryAsync(string owner, string repo);
    Task<GitHubRepository?> CreateRepositoryAsync(string name, string? description, bool isPrivate);
    Task<bool> RepositoryExistsAsync(string owner, string repo);
}
