using System.Text.Json.Serialization;

namespace GitUploadTool.Models;

public class RecentProject
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("repoUrl")]
    public string? RepoUrl { get; set; }

    [JsonPropertyName("uploadTime")]
    public DateTime UploadTime { get; set; }

    [JsonPropertyName("branch")]
    public string Branch { get; set; } = "main";
}
