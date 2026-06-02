using System.IO;
using System.Text.Json;
using GitUploadTool.Models;
using NLog;

namespace GitUploadTool.Services;

public class SettingsService : ISettingsService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GitUploadTool");
    private static readonly string ConfigFile = Path.Combine(AppDataPath, "config.json");

    public async Task<AppSettings> GetSettingsAsync()
    {
        try
        {
            if (!File.Exists(ConfigFile))
                return new AppSettings();

            var json = await File.ReadAllTextAsync(ConfigFile);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to read settings");
            return new AppSettings();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppDataPath);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(ConfigFile, json);
            Logger.Info("Settings saved");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save settings");
            throw;
        }
    }
}
