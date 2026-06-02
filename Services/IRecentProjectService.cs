using GitUploadTool.Models;

namespace GitUploadTool.Services;

public interface IRecentProjectService
{
    Task<List<RecentProject>> GetRecentProjectsAsync();
    Task AddRecentProjectAsync(RecentProject project);
    Task ClearRecentProjectsAsync();
}
