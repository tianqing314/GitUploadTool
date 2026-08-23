using System.IO;
using GitUploadTool.Models;
using NLog;

namespace GitUploadTool.Services;

public class GitIgnoreService : IGitIgnoreService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly string TemplatesDir = Path.Combine(
        AppContext.BaseDirectory, "wwwroot", "templates", "gitignore");

    private List<GitIgnoreTemplate>? _templatesCache;

    public List<GitIgnoreTemplate> GetTemplates()
    {
        if (_templatesCache != null)
            return _templatesCache;

        var templates = new List<GitIgnoreTemplate>();
        try
        {
            if (!Directory.Exists(TemplatesDir))
            {
                Logger.Warn($"Templates directory not found: {TemplatesDir}");
                _templatesCache = templates;
                return templates;
            }

            foreach (var file in Directory.GetFiles(TemplatesDir, "*.gitignore"))
            {
                try
                {
                    // 文件名格式: {language}.gitignore，如 csharp.gitignore
                    var language = Path.GetFileNameWithoutExtension(file);
                    var content = File.ReadAllText(file);

                    templates.Add(new GitIgnoreTemplate
                    {
                        Name = GetDisplayName(language),
                        Language = language,
                        Content = content,
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to load template file: {file}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load gitignore templates");
        }

        _templatesCache = templates;
        return templates;
    }

    public GitIgnoreTemplate? GetTemplate(string language)
    {
        return GetTemplates().FirstOrDefault(t =>
            t.Language.Equals(language, StringComparison.OrdinalIgnoreCase));
    }

    public async Task ApplyTemplateAsync(string projectPath, string language)
    {
        try
        {
            var template = GetTemplate(language);
            if (template == null)
            {
                Logger.Warn($"Template not found for language: {language}");
                return;
            }

            var gitIgnorePath = Path.Combine(projectPath, ".gitignore");

            // 若已存在 .gitignore 则追加（带分隔注释），避免覆盖用户已有配置
            if (File.Exists(gitIgnorePath))
            {
                var existing = await File.ReadAllTextAsync(gitIgnorePath);
                // 若模板关键条目已存在则跳过追加
                if (!existing.Contains(template.Content.Substring(0, Math.Min(50, template.Content.Length))))
                {
                    await File.AppendAllTextAsync(gitIgnorePath,
                        $"{Environment.NewLine}{Environment.NewLine}# --- GitUploadTool: applied template '{template.Name}' ---{Environment.NewLine}{template.Content}");
                    Logger.Info($"Appended .gitignore template: {template.Name}");
                }
                else
                {
                    Logger.Info($".gitignore already contains template '{template.Name}', skipping append");
                }
            }
            else
            {
                await File.WriteAllTextAsync(gitIgnorePath, template.Content);
                Logger.Info($"Applied .gitignore template: {template.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply .gitignore template");
            throw;
        }
    }

    private static string GetDisplayName(string language) => language.ToLowerInvariant() switch
    {
        "csharp" => "C# / .NET",
        "visualstudio" => "Visual Studio",
        "node" => "Node.js",
        "python" => "Python",
        "java" => "Java",
        _ => language,
    };
}
