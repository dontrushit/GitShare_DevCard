namespace GitShare.Api.Services;

/// <summary>
/// Выбор 2–3 ключевых файлов для передачи в LLM (только Production App).
/// </summary>
internal static class LlmEvidenceFileSelector
{
    public const int MaxFilesForLlm = 2;
    public const int MaxCharsPerFile = 1200;

    private static readonly string[] InitializationFileNames =
    [
        "Program.cs",
        "Startup.cs",
        "App.xaml.cs",
        "MainWindow.xaml.cs",
        "Main.cs",
        "Application.java"
    ];

    private static readonly string[] SuspiciousNameFragments =
    [
        "DbHelper",
        "DataHelper",
        "SqlHelper",
        "Utils",
        "Utility",
        "Manager",
        "Helper"
    ];

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".java",
        ".kt",
        ".fs",
        ".vb"
    };

    public static List<string> SelectPaths(
        string repoName,
        IReadOnlyList<string> blobPaths,
        string signatureManifest,
        string projectClass)
    {
        var effectiveClass = ProjectClassProsCons.ResolveEffectiveClass(projectClass, repoName, signatureManifest);
        if (!EnterpriseAuditLexicon.IsProductionClass(effectiveClass))
        {
            return [];
        }

        if (blobPaths.Count == 0)
        {
            return [];
        }

        var normalized = blobPaths
            .Select(p => p.Replace('\\', '/'))
            .Where(IsEligibleSourcePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            return [];
        }

        var scored = normalized
            .Select(path => (Path: path, Score: ScorePath(path, signatureManifest)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selected = new List<string>();

        foreach (var name in InitializationFileNames)
        {
            if (selected.Count >= MaxFilesForLlm)
            {
                break;
            }

            var match = normalized.FirstOrDefault(p =>
                Path.GetFileName(p).Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !selected.Contains(match, StringComparer.OrdinalIgnoreCase))
            {
                selected.Add(match);
            }
        }

        foreach (var (path, score) in scored.Where(x => x.Score >= 50))
        {
            if (selected.Count >= MaxFilesForLlm)
            {
                break;
            }

            if (!selected.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                selected.Add(path);
            }
        }

        foreach (var (path, _) in scored)
        {
            if (selected.Count >= MaxFilesForLlm)
            {
                break;
            }

            if (!selected.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                selected.Add(path);
            }
        }

        return selected.Take(MaxFilesForLlm).ToList();
    }

    private static bool IsEligibleSourcePath(string path)
    {
        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            return false;
        }

        if (path.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static int ScorePath(string path, string manifest)
    {
        var score = 0;
        var fileName = Path.GetFileName(path);
        var depth = path.Count(c => c == '/');

        if (InitializationFileNames.Any(n => fileName.Equals(n, StringComparison.OrdinalIgnoreCase)))
        {
            score += 100;
        }

        if (SuspiciousNameFragments.Any(frag => fileName.Contains(frag, StringComparison.OrdinalIgnoreCase)))
        {
            score += 80;
            if (depth <= 1)
            {
                score += 20;
            }
        }

        if (path.Contains("/Controllers/", StringComparison.OrdinalIgnoreCase) &&
            fileName.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase) &&
            !ProjectStackDetector.IsUnityAssetPath(path))
        {
            score += 45;
        }

        if (path.Contains("/Services/", StringComparison.OrdinalIgnoreCase) &&
            fileName.EndsWith("Service.cs", StringComparison.OrdinalIgnoreCase))
        {
            score += 40;
        }

        if (ManifestSignalParser.DetectedKeyFilesContain(manifest, fileName))
        {
            score += 35;
        }

        if (fileName.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase))
        {
            score -= 100;
        }

        return score;
    }
}
