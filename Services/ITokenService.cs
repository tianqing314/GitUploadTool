namespace GitUploadTool.Services;

public interface ITokenService
{
    Task<string?> GetTokenAsync();
    Task SaveTokenAsync(string token);
    Task DeleteTokenAsync();
    Task<bool> HasTokenAsync();
}
