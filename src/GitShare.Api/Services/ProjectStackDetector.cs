namespace GitShare.Api.Services;

/// <summary>
/// Определяет тип проекта по дереву файлов (без чтения содержимого).
/// </summary>
internal static class ProjectStackDetector
{
    public static bool IsUnityProject(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return false;
        }

        var hasProjectSettings = paths.Any(p =>
            p.Contains("ProjectSettings/", StringComparison.OrdinalIgnoreCase));
        var hasUnityAssets = paths.Any(p =>
            p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase));
        var hasUnityScenes = paths.Any(p =>
            p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));
        var hasPackagesManifest = paths.Any(p =>
            p.Equals("Packages/manifest.json", StringComparison.OrdinalIgnoreCase));

        return hasProjectSettings || hasUnityScenes || (hasUnityAssets && hasPackagesManifest);
    }

    public static bool IsWebAspNetProject(IReadOnlyList<string> paths)
    {
        if (IsUnityProject(paths))
        {
            return false;
        }

        if (paths.Any(p => p.EndsWith("web.config", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (paths.Any(p =>
                p.Contains("/Controllers/", StringComparison.OrdinalIgnoreCase) &&
                !IsUnderUnityAssets(p)))
        {
            return true;
        }

        var hasProgram = paths.Any(p =>
            Path.GetFileName(p).Equals("Program.cs", StringComparison.OrdinalIgnoreCase));
        var hasAppsettings = paths.Any(p =>
            p.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase));

        return hasProgram && hasAppsettings;
    }

    public static bool IsUnityAssetPath(string path) => IsUnderUnityAssets(path);

    public static bool IsUnityMvcStyleController(string path) =>
        path.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase) &&
        IsUnderUnityAssets(path);

    public static bool HasUnityPluginLayout(IReadOnlyList<string> paths) =>
        paths.Any(p => p.Contains("/Plugins/", StringComparison.OrdinalIgnoreCase) &&
                       p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase));

    public static bool HasUnitySamplePatternFolders(IReadOnlyList<string> paths)
    {
        static bool HasFolder(IReadOnlyList<string> all, string segment) =>
            all.Any(p =>
                p.Contains($"/{segment}/", StringComparison.OrdinalIgnoreCase) &&
                p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase));

        var patternFolders = HasFolder(paths, "MVC") || HasFolder(paths, "MV") || HasFolder(paths, "Flat");
        return patternFolders && paths.Count(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) >= 3;
    }

    public static bool HasUnityTestLayout(IReadOnlyList<string> paths) =>
        paths.Any(p =>
            p.Contains("/Tests/", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("/EditModeTests/", StringComparison.OrdinalIgnoreCase) ||
            p.Contains(".Tests/", StringComparison.OrdinalIgnoreCase) ||
            p.EndsWith(".Tests.cs", StringComparison.OrdinalIgnoreCase));

    public static List<string> SelectUnityKeyFileNames(IReadOnlyList<string> paths, int maxCount = 10)
    {
        static int Score(string path)
        {
            var score = 0;
            if (path.Contains("/Plugins/", StringComparison.OrdinalIgnoreCase))
            {
                score += 12;
            }

            if (path.Contains("/Core/", StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }

            if (path.Contains("/Editor/", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith("Editor.cs", StringComparison.OrdinalIgnoreCase))
            {
                score += 8;
            }

            if (path.Contains("/Scripts/", StringComparison.OrdinalIgnoreCase))
            {
                score += 6;
            }

            var fileName = Path.GetFileName(path);
            if (fileName.Contains("Generator", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("Processor", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("Analyzer", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("Installer", StringComparison.OrdinalIgnoreCase))
            {
                score += 7;
            }

            if (fileName.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith("View.cs", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("Wallet.cs", StringComparison.OrdinalIgnoreCase))
            {
                score += 5;
            }

            if (path.Contains("/Samples/", StringComparison.OrdinalIgnoreCase))
            {
                score -= 4;
            }

            return score;
        }

        return paths
            .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(p => p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(Score)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToList();
    }

    private static bool IsUnderUnityAssets(string path) =>
        path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/Assets/", StringComparison.OrdinalIgnoreCase);
}
