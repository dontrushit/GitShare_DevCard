using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>
/// Классификация фреймворков и языков по техническим семействам для оценки широты портфеля.
/// </summary>
internal static class PortfolioStackTaxonomy
{
    private static readonly string[] DevOpsMarkers =
    [
        "devops", "terraform", "ansible", "kubernetes", "k8s", "docker", "pulumi",
        "helm", "cloudformation", "yaml", "infrastructure"
    ];

    public static bool IsDevOpsFramework(string framework)
    {
        if (string.IsNullOrWhiteSpace(framework))
        {
            return false;
        }

        return DevOpsMarkers.Any(marker =>
            framework.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    public static HashSet<string> CollectStackFamilies(StructuredAuditResponse? audit)
    {
        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (audit?.Projects is not { Count: > 0 } projects)
        {
            return families;
        }

        foreach (var project in projects)
        {
            var family = ClassifyStackFamily(project.Framework, project.RepoName);
            if (!string.Equals(family, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                families.Add(family);
            }
        }

        return families;
    }

    public static string ClassifyLanguageFamily(string language) =>
        language.Trim().ToLowerInvariant() switch
        {
            "c#" or "csharp" => ".NET",
            "java" => "Java",
            "javascript" or "typescript" => "JS/TS",
            "python" => "Python",
            "go" => "Go",
            "c" or "c++" => "C/C++",
            "html" or "css" => "Web",
            "ruby" => "Ruby",
            "rust" => "Rust",
            "kotlin" => "Kotlin",
            "shell" or "powershell" => "DevOps",
            "dockerfile" or "hcl" => "DevOps",
            _ => string.IsNullOrWhiteSpace(language) ? "unknown" : language
        };

    public static string ClassifyStackFamily(string framework, string repoName = "")
    {
        var combined = $"{framework} {repoName}";

        if (IsDevOpsFramework(combined) ||
            combined.Contains("ansible", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("terraform", StringComparison.OrdinalIgnoreCase))
        {
            return "DevOps";
        }

        if (combined.Contains("playwright", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("selenium", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("cypress", StringComparison.OrdinalIgnoreCase))
        {
            return "Test automation";
        }

        if (combined.Contains("python", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("django", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("flask", StringComparison.OrdinalIgnoreCase) ||
            repoName.Contains("bot", StringComparison.OrdinalIgnoreCase))
        {
            return "Python";
        }

        if (combined.Contains("javascript", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("typescript", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("node", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("react", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("laravel", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("php", StringComparison.OrdinalIgnoreCase))
        {
            return "JS/TS";
        }

        if (combined.Contains("go ", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("golang", StringComparison.OrdinalIgnoreCase) ||
            framework.Equals("Go", StringComparison.OrdinalIgnoreCase))
        {
            return "Go";
        }

        if (framework.Contains("WinForms", StringComparison.OrdinalIgnoreCase))
        {
            return "WinForms";
        }

        if (framework.Contains("WPF", StringComparison.OrdinalIgnoreCase))
        {
            return "WPF";
        }

        if (framework.Contains("Spring", StringComparison.OrdinalIgnoreCase) ||
            framework.Contains("Java", StringComparison.OrdinalIgnoreCase))
        {
            return "Java";
        }

        if (framework.Contains("Unity", StringComparison.OrdinalIgnoreCase))
        {
            return "Unity";
        }

        if (framework.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase))
        {
            return "ASP.NET";
        }

        if (framework.Contains("Console", StringComparison.OrdinalIgnoreCase) ||
            framework.Equals(".NET", StringComparison.OrdinalIgnoreCase))
        {
            return "Console/.NET";
        }

        return IsUndefinedFramework(framework) ? "unknown" : ".NET";
    }

    private static bool IsUndefinedFramework(string framework) =>
        string.IsNullOrWhiteSpace(framework) ||
        framework.Contains("не определён", StringComparison.OrdinalIgnoreCase);
}
