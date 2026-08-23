using GitUploadTool.Models;

namespace GitUploadTool.Services;

/// <summary>
/// 平台服务抽象接口（支持 GitHub、GitLab 等）
/// </summary>
public interface IPlatformService
{
    /// <summary>平台名称</summary>
    string PlatformName { get; }

    /// <summary>API 基础地址</summary>
    string BaseUrl { get; }

    /// <summary>获取当前用户信息（统一返回 GitHubUser 模型）</summary>
    Task<GitHubUser?> GetUserAsync();

    /// <summary>获取用户的所有仓库</summary>
    Task<List<RepositoryInfo>> GetPlatformRepositoriesAsync();

    /// <summary>获取仓库信息</summary>
    Task<RepositoryInfo?> GetPlatformRepositoryAsync(string owner, string name);

    /// <summary>创建仓库</summary>
    Task<RepositoryInfo?> CreatePlatformRepositoryAsync(string name, string? description, bool isPrivate);

    /// <summary>删除仓库</summary>
    Task<bool> DeleteRepositoryAsync(string owner, string name);

    /// <summary>修改仓库私有状态</summary>
    Task<bool> UpdateRepoVisibilityAsync(string owner, string name, bool isPrivate);

    /// <summary>验证 Token 是否有效</summary>
    Task<bool> ValidateTokenAsync(string token);
}

/// <summary>
/// 仓库信息（平台无关的通用模型）
/// </summary>
public class RepositoryInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CloneUrl { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string DefaultBranch { get; set; } = "main";
}