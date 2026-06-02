using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using GitUploadTool.Models;
using Microsoft.Extensions.Configuration;
using NLog;

namespace GitUploadTool.Services;

public class AuthenticationService : IAuthenticationService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private HttpListener? _listener;
    private string? _pendingCode;

    public AuthenticationService(ITokenService tokenService, IConfiguration configuration, HttpClient httpClient)
    {
        _tokenService = tokenService;
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task<bool> LoginAsync()
    {
        try
        {
            var clientId = _configuration["GitHub:ClientId"];
            var clientSecret = _configuration["GitHub:ClientSecret"];
            var port = GetAvailablePort();

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) ||
                clientId == "YOUR_CLIENT_ID")
            {
                Logger.Error("GitHub OAuth credentials not configured. Please set ClientId and ClientSecret in appsettings.json");
                return false;
            }

            var redirectUri = $"http://localhost:{port}/callback";
            var state = Guid.NewGuid().ToString("N");
            var authUrl = $"https://github.com/login/oauth/authorize?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=repo,user&state={state}";

            // Start listening for callback
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
            Logger.Info($"Listening for OAuth callback on port {port}");

            // Open browser
            Process.Start(new ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });

            // Wait for callback
            var context = await _listener.GetContextAsync();
            var code = context.Request.QueryString["code"];
            var returnedState = context.Request.QueryString["state"];

            // Send response to browser
            var responseHtml = "<html><body><h1>Authorization successful!</h1><p>You can close this window.</p></body></html>";
            var buffer = Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer);
            context.Response.Close();
            _listener.Stop();

            if (string.IsNullOrEmpty(code) || returnedState != state)
            {
                Logger.Error("Invalid OAuth callback");
                return false;
            }

            // Exchange code for token
            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "code", code }
                })
            };
            tokenRequest.Headers.Add("Accept", "application/json");

            var tokenResponse = await _httpClient.SendAsync(tokenRequest);
            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);

            if (tokenData.TryGetProperty("access_token", out var tokenElement))
            {
                var token = tokenElement.GetString();
                if (!string.IsNullOrEmpty(token))
                {
                    await _tokenService.SaveTokenAsync(token);
                    Logger.Info("GitHub login successful");
                    return true;
                }
            }

            Logger.Error("Failed to get access token from GitHub");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Login failed");
            return false;
        }
        finally
        {
            _listener?.Close();
        }
    }

    public async Task LogoutAsync()
    {
        await _tokenService.DeleteTokenAsync();
        Logger.Info("Logged out");
    }

    public async Task<GitHubUser?> GetCurrentUserAsync()
    {
        try
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
                return null;

            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            request.Headers.Add("Authorization", $"Bearer {token}");
            request.Headers.Add("User-Agent", "GitUploadTool");

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<GitHubUser>(json);
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to get current user");
            return null;
        }
    }

   public async Task<bool> IsAuthenticatedAsync()
   {
       var token = await _tokenService.GetTokenAsync();
       return !string.IsNullOrEmpty(token);
   }

    private int GetAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
