using System.IO;
using System.Text.Json;
using GitUploadTool.Models;
using NLog;

namespace GitUploadTool.Services;

public class RecentProjectService : IRecentProjectService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GitUploadTool");
    private static readonly string RecentFile = Path.Combine(AppDataPath, "recent.json");
    private const int MaxRecentProjects = 20;

    public async Task<List<RecentProject>> GetRecentProjectsAsync()
    {
        try
        {
            if (!File.Exists(RecentFile))
                return new List<RecentProject>();

            var json = await File.ReadAllTextAsync(RecentFile);
            return JsonSerializer.Deserialize<List<RecentProject>>(json) ?? new List<RecentProject>();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to read recent projects");
            return new List<RecentProject>();
        }
    }

    public async Task AddRecentProjectAsync(RecentProject project)
    {
        try
        {
            var projects = await GetRecentProjectsAsync();
            
            // Remove existing entry for same path
            projects.RemoveAll(p => p.Path == project.Path);
            
            // Add to beginning
            projects.Insert(0, project);
            
            // Limit count
            if (projects.Count > MaxRecentProjects)
                projects = projects.Take(MaxRecentProjects).ToList();

            Directory.CreateDirectory(AppDataPath);
            var json = JsonSerializer.Serialize(projects, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(RecentFile, json);
            Logger.Info($"Added recent project: {project.Name}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to add recent project");
        }
    }

    public async Task ClearRecentProjectsAsync()
    {
        try
        {
            if (File.Exists(RecentFile))
                File.Delete(RecentFile);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to clear recent projects");
        }
    }
}
