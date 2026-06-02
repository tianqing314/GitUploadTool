using GitUploadTool.Models;

namespace GitUploadTool.Services;

public interface IAuthenticationService
{
    Task<bool> LoginAsync();
    Task LogoutAsync();
    Task<GitHubUser?> GetCurrentUserAsync();
    Task<bool> IsAuthenticatedAsync();
}
