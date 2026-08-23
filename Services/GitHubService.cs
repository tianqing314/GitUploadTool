using System.Net.Http;
using System.Text;
using System.Text.Json;
using GitUploadTool.Models;
using NLog;

namespace GitUploadTool.Services;

public class GitHubService : IGitHubService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ITokenService _tokenService;
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.github.com";

    public GitHubService(ITokenService tokenService, HttpClient httpClient)
    {
        _tokenService = tokenService;
        _httpClient = httpClient;
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url)
    {
        var token = await _tokenService.GetTokenAsync();
        var request = new HttpRequestMessage(method, $"{BaseUrl}{url}");
        request.Headers.Add("Authorization", $"token {token}");
        request.Headers.Add("User-Agent", "GitUploadTool");
        request.Headers.Add("Accept", "application/vnd.github.v3+json");
        return request;
    }

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
}
