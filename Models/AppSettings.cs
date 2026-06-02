using System.Text.Json.Serialization;

namespace GitUploadTool.Models;

public class AppSettings
{
    [JsonPropertyName("proxyAddress")]
    public string? ProxyAddress { get; set; }

    [JsonPropertyName("proxyPort")]
    public int? ProxyPort { get; set; }

    [JsonPropertyName("defaultBranch")]
    public string DefaultBranch { get; set; } = "main";

    [JsonPropertyName("defaultCommitMessage")]
    public string DefaultCommitMessage { get; set; } = "Update from GitUploadTool";

    [JsonPropertyName("autoPush")]
    public bool AutoPush { get; set; } = true;
}
