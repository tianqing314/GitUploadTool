using GitUploadTool.Models;

namespace GitUploadTool.Services;

public interface IGitIgnoreService
{
    List<GitIgnoreTemplate> GetTemplates();
    GitIgnoreTemplate? GetTemplate(string language);
    Task ApplyTemplateAsync(string projectPath, string language);
}
