using System.Net.Http;
using System.Text;
using System.Text.Json;
using GitUploadTool.Models;
using NLog;

namespace GitUploadTool.Services;

/// <summary>
/// GitHub 平台服务实现
/// </summary>
public class GitHubService : IGitHubService, IPlatformService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ITokenService _tokenService;
    private readonly HttpClient _httpClient;
    private const string BaseUrlConst = "https://api.github.com";

    public GitHubService(ITokenService tokenService, HttpClient httpClient)
    {
        _tokenService = tokenService;
        _httpClient = httpClient;
    }

    // ===== IPlatformService 元数据 =====
    public string PlatformName => "GitHub";
    public string BaseUrl => BaseUrlConst;
    public string AuthUrl => "https://github.com/login/oauth/authorize";
    public string TokenUrl => "https://github.com/login/oauth/access_token";
    public string UserAgent => "GitUploadTool";
    public string AcceptHeader => "application/vnd.github.v3+json";

    // ===== 共用请求构造 =====
    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url)
    {
        var token = await _tokenService.GetTokenAsync();
        var request = new HttpRequestMessage(method, $"{BaseUrl}{url}");
        request.Headers.Add("Authorization", $"token {token}");
        request.Headers.Add("User-Agent", UserAgent);
        request.Headers.Add("Accept", AcceptHeader);
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
                return JsonSerializer.Deserialize<GitHubUser>(json);
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

    public async Task<List<RepositoryInfo>> GetPlatformRepositoriesAsync()
    {
        var repos = await GetRepositoriesAsync();
        return repos.Select(r => new RepositoryInfo
        {
            Name = r.Name ?? "",
            FullName = r.FullName ?? "",
            Description = r.Description ?? "",
            CloneUrl = r.HtmlUrl ?? "",
            HtmlUrl = r.HtmlUrl ?? "",
            IsPrivate = r.Private,
            DefaultBranch = "main",
        }).ToList();
    }

    public async Task<RepositoryInfo?> GetPlatformRepositoryAsync(string owner, string name)
    {
        var repo = await GetRepositoryAsync(owner, name);
        if (repo == null) return null;

        return new RepositoryInfo
        {
            Name = repo.Name ?? "",
            FullName = repo.FullName ?? "",
            Description = repo.Description ?? "",
            CloneUrl = repo.HtmlUrl ?? "",
            HtmlUrl = repo.HtmlUrl ?? "",
            IsPrivate = repo.Private,
        };
    }

    public async Task<RepositoryInfo?> CreatePlatformRepositoryAsync(string name, string? description, bool isPrivate)
    {
        var repo = await CreateRepositoryAsync(name, description, isPrivate);
        if (repo == null) return null;

        return new RepositoryInfo
        {
            Name = repo.Name ?? "",
            FullName = repo.FullName ?? "",
            Description = repo.Description ?? "",
            CloneUrl = repo.HtmlUrl ?? "",
            HtmlUrl = repo.HtmlUrl ?? "",
            IsPrivate = repo.Private,
        };
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/user");
            request.Headers.Add("Authorization", $"token {token}");
            request.Headers.Add("User-Agent", UserAgent);
            request.Headers.Add("Accept", AcceptHeader);

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ValidateToken failed");
            return false;
        }
    }

    // ===== IGitHubService 原有接口实现 =====
    public async Task<List<GitHubRepository>> GetRepositoriesAsync()
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Get, "/user/repos?sort=updated&per_page=100");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var repos = JsonSerializer.Deserialize<List<GitHubRepository>>(json);
                return repos ?? new List<GitHubRepository>();
            }

            Logger.Warn($"Failed to get repositories: {response.StatusCode}");
            return new List<GitHubRepository>();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to get repositories");
            return new List<GitHubRepository>();
        }
    }

    public async Task<GitHubRepository?> GetRepositoryAsync(string owner, string repo)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Get, $"/repos/{owner}/{repo}");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<GitHubRepository>(json);
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to get repository {owner}/{repo}");
            return null;
        }
    }

    public async Task<GitHubRepository?> CreateRepositoryAsync(string name, string? description, bool isPrivate)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Post, "/user/repos");
            var body = new
            {
                name,
                description,
                @private = isPrivate,
                auto_init = true
            };
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Logger.Info($"Repository {name} created successfully");
                return JsonSerializer.Deserialize<GitHubRepository>(json);
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

    public async Task<bool> RepositoryExistsAsync(string owner, string repo)
    {
        var repository = await GetRepositoryAsync(owner, repo);
        return repository != null;
    }

    public async Task<(bool Success, string? Error)> DeleteRepositoryAsync(string owner, string repo)
    {
        try
        {
            var request = await CreateRequestAsync(HttpMethod.Delete, $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}");
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return (true, null);

            var body = await response.Content.ReadAsStringAsync();
            Logger.Warn("Delete repo failed: {Status} {Body}", (int)response.StatusCode, body);
            return (false, $"HTTP {(int)response.StatusCode}: {body}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to delete repository {owner}/{repo}");
            return (false, ex.Message);
        }
    }

    public async Task<bool> UpdateRepoVisibilityAsync(string owner, string repo, bool isPrivate)
    {
        try
        {
            var request = await CreateRequestAsync(new HttpMethod("PATCH"), $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}");
            var body = new { @private = isPrivate };
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to update repository visibility {owner}/{repo}");
            return false;
        }
    }
}
