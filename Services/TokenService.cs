using System.IO;
using System.Text.Json;
using NLog;

namespace GitUploadTool.Services;

public class TokenService : ITokenService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GitUploadTool");
    private static readonly string TokenFile = Path.Combine(AppDataPath, "token.json");

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            if (!File.Exists(TokenFile))
                return null;

            var json = await File.ReadAllTextAsync(TokenFile);
            var data = JsonSerializer.Deserialize<TokenData>(json);
            return data?.AccessToken;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to read token");
            return null;
        }
    }

    public async Task SaveTokenAsync(string token)
    {
        try
        {
            Directory.CreateDirectory(AppDataPath);
            var data = new TokenData { AccessToken = token, SavedAt = DateTime.Now };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(TokenFile, json);
            Logger.Info("Token saved successfully");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save token");
            throw;
        }
    }

    public async Task DeleteTokenAsync()
    {
        try
        {
            if (File.Exists(TokenFile))
            {
                File.Delete(TokenFile);
                Logger.Info("Token deleted");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to delete token");
            throw;
        }
    }

    public async Task<bool> HasTokenAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    private class TokenData
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; }
    }
}
