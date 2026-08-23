using System.Net.Http;
using System.Text;
using System.Text.Json;
using GitUploadTool.Models;
using NLog;

namespace GitUploadTool.Services;

/// <summary>
/// GitLab 平台服务实现
/// </summary>
public class GitLabService : IPlatformService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ITokenService _tokenService;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public GitLabService(ITokenService tokenService, HttpClient httpClient, string baseUrl = "https://gitlab.com")
    {
        _tokenService = tokenService;
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    // ===== IPlatformService 元数据 =====
    public string PlatformName => "GitLab";
    public string BaseUrl => $"{_baseUrl}/api/v4";
    public string AuthUrl => $"{_baseUrl}/oauth/authorize";
    public string TokenUrl => $"{_baseUrl}/oauth/token";
    public string UserAgent => "GitUploadTool";
    public string AcceptHeader => "application/json";

    // ===== 共用请求构造 =====
    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url)
    {
        var token = await _tokenService.GetTokenAsync();
        var request = new HttpRequestMessage(method, $"{BaseUrl}{url}");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Headers.Add("User-Agent", UserAgent);
        return request;
    }

    // ===== IPlatformService =====
    public async Task<GitHubUser?> GetUserAsync()
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Get, "/user");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var user = JsonSerializer.Deserialize<GitLabUser>(json);
                return user != null ? new GitHubUser
                {
                    Login = user.Username,
                    Name = user.Name,
                    Email = user.Email,
                    AvatarUrl = user.AvatarUrl
                } : null;
            }

            Logger.Warn($"Failed to get user: {response.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to get user");
            return null;
        }
    }

    public async Task<List<RepositoryInfo>> GetRepositoriesAsync()
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Get, "/projects?membership=true&per_page=100");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var projects = JsonSerializer.Deserialize<List<GitLabProject>>(json);
                return projects?.Select(p => new RepositoryInfo
                {
                    Name = p.Path ?? "",
                    FullName = p.PathWithNamespace ?? "",
                    Description = p.Description ?? "",
                    CloneUrl = p.HttpUrlToRepo ?? p.SshUrlToRepo ?? "",
                    HtmlUrl = p.WebUrl ?? "",
                    IsPrivate = p.Visibility?.ToLower() == "private",
                    DefaultBranch = p.DefaultBranch ?? "main",
                }).ToList() ?? new List<RepositoryInfo>();
            }

            Logger.Warn($"Failed to get repositories: {response.StatusCode}");
            return new List<RepositoryInfo>();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to get repositories");
            return new List<RepositoryInfo>();
        }
    }

    public async Task<RepositoryInfo?> GetRepositoryAsync(string owner, string name)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Get, $"/projects/{Uri.EscapeDataString(owner)}%2F{Uri.EscapeDataString(name)}");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var project = JsonSerializer.Deserialize<GitLabProject>(json);
                if (project != null)
                {
                    return new RepositoryInfo
                    {
                        Name = project.Path ?? "",
                        FullName = project.PathWithNamespace ?? "",
                        Description = project.Description ?? "",
                        CloneUrl = project.HttpUrlToRepo ?? project.SshUrlToRepo ?? "",
                        HtmlUrl = project.WebUrl ?? "",
                        IsPrivate = project.Visibility?.ToLower() == "private",
                        DefaultBranch = project.DefaultBranch ?? "main",
                    };
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to get repository {owner}/{name}");
            return null;
        }
    }

    public async Task<RepositoryInfo?> CreateRepositoryAsync(string name, string description, bool isPrivate)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Post, "/projects");
            var body = new
            {
                name,
                description,
                visibility = isPrivate ? "private" : "public",
                initialize_with_readme = true
            };
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var project = JsonSerializer.Deserialize<GitLabProject>(json);
                Logger.Info($"Repository {name} created successfully");
                return new RepositoryInfo
                {
                    Name = project?.Path ?? name,
                    FullName = project?.PathWithNamespace ?? $"{name}",
                    Description = project?.Description ?? description,
                    CloneUrl = project?.HttpUrlToRepo ?? "",
                    HtmlUrl = project?.WebUrl ?? "",
                    IsPrivate = isPrivate,
                    DefaultBranch = project?.DefaultBranch ?? "main",
                };
            }

            var error = await response.Content.ReadAsStringAsync();
            Logger.Error($"Failed to create repository: {error}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to create repository {name}");
            return null;
        }
    }

    public async Task<List<RepositoryInfo>> GetPlatformRepositoriesAsync()
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Get, "/projects?membership=true&per_page=100");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var projects = JsonSerializer.Deserialize<List<GitLabProject>>(json);
                return projects?.Select(p => new RepositoryInfo
                {
                    Name = p.Path ?? "",
                    FullName = p.PathWithNamespace ?? "",
                    Description = p.Description ?? "",
                    CloneUrl = p.HttpUrlToRepo ?? p.SshUrlToRepo ?? "",
                    HtmlUrl = p.WebUrl ?? "",
                    IsPrivate = p.Visibility?.ToLower() == "private",
                    DefaultBranch = p.DefaultBranch ?? "main",
                    CreatedAt = p.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = p.UpdatedAt ?? DateTime.UtcNow,
                }).ToList() ?? new List<RepositoryInfo>();
            }

            Logger.Warn($"Failed to get repositories: {response.StatusCode}");
            return new List<RepositoryInfo>();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to get repositories");
            return new List<RepositoryInfo>();
        }
    }

    public async Task<RepositoryInfo?> GetPlatformRepositoryAsync(string owner, string name)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Get, $"/projects/{Uri.EscapeDataString(owner)}%2F{Uri.EscapeDataString(name)}");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var project = JsonSerializer.Deserialize<GitLabProject>(json);
                if (project != null)
                {
                    return new RepositoryInfo
                    {
                        Name = project.Path ?? "",
                        FullName = project.PathWithNamespace ?? "",
                        Description = project.Description ?? "",
                        CloneUrl = project.HttpUrlToRepo ?? project.SshUrlToRepo ?? "",
                        HtmlUrl = project.WebUrl ?? "",
                        IsPrivate = project.Visibility?.ToLower() == "private",
                        DefaultBranch = project.DefaultBranch ?? "main",
                        CreatedAt = project.CreatedAt ?? DateTime.UtcNow,
                        UpdatedAt = project.UpdatedAt ?? DateTime.UtcNow,
                    };
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to get repository {owner}/{name}");
            return null;
        }
    }

    public async Task<RepositoryInfo?> CreatePlatformRepositoryAsync(string name, string? description, bool isPrivate)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Post, "/projects");
            var body = new
            {
                name,
                description,
                visibility = isPrivate ? "private" : "public",
                initialize_with_readme = true
            };
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var project = JsonSerializer.Deserialize<GitLabProject>(json);
                Logger.Info($"Repository {name} created successfully");
                return new RepositoryInfo
                {
                    Name = project?.Path ?? name,
                    FullName = project?.PathWithNamespace ?? $"{name}",
                    Description = project?.Description ?? description ?? "",
                    CloneUrl = project?.HttpUrlToRepo ?? "",
                    HtmlUrl = project?.WebUrl ?? "",
                    IsPrivate = isPrivate,
                    DefaultBranch = project?.DefaultBranch ?? "main",
                    CreatedAt = project?.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = project?.UpdatedAt ?? DateTime.UtcNow,
                };
            }

            var error = await response.Content.ReadAsStringAsync();
            Logger.Error($"Failed to create repository: {error}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to create repository {name}");
            return null;
        }
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/user");
            request.Headers.Add("Authorization", $"Bearer {token}");
            request.Headers.Add("User-Agent", UserAgent);

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ValidateToken failed");
            return false;
        }
    }

    // ===== GitLab 专用模型 =====
    private class GitLabUser
    {
        public string? Username { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? AvatarUrl { get; set; }
    }

    private class GitLabProject
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Path { get; set; }
        public string? PathWithNamespace { get; set; }
        public string? Description { get; set; }
        public string? DefaultBranch { get; set; }
        public string? Visibility { get; set; }
        public string? HttpUrlToRepo { get; set; }
        public string? SshUrlToRepo { get; set; }
        public string? WebUrl { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
