namespace GitShare.Api.Services;

/// <summary>
/// Эвристики для Unity-репозиториев: отсев Asset Store, учебные примеры, Composition Root.
/// </summary>
internal static class UnityRepositoryHeuristics
{
    private static readonly string[] AssetPackPathMarkers =
    [
        "/ETFX/",
        "ETFX",
        "Epic Toon",
        "ParticleEffectsLibrary",
        "RoadGenerator.cs",
        "AssetStore",
        "/Standard Assets/",
        "/TextMesh Pro/",
        "Toon/",
        "Cartoon FX"
    ];

    private static readonly string[] AssetPackFileNames =
    [
        "ETFXButtonScript.cs",
        "ETFXFireProjectile.cs",
        "ETFXLoopScript.cs",
        "ParticleEffectsLibrary.cs",
        "PEButtonScript.cs"
    ];

    private static readonly string[] LearningRepoNameTokens =
    [
        "example",
        "examples",
        "homework",
        "tutorial",
        "beginner",
        "course",
        "learn",
        "sample",
        "demo",
        "prototype",
        "petting-system",
        "youtube-example"
    ];

    private static readonly string[] QualityRepoNameTokens =
    [
        "typed-scenes",
        "architecture-examples",
        "asteroids",
        "amigos",
        "unity-typed"
    ];

    private static readonly string[] LowValueRepoNameTokens =
    [
        "homework",
        "beginner",
        "snake-3d",
        "camp-runner",
        "agressive-driver",
        "aggressive-driver",
        "ijunior-world",
        "live_executer"
    ];

    /// <summary>Крупные репо с ассетами (KB), штраф при отборе на аудит.</summary>
    public const int LargeRepoSizeKbThreshold = 20_000;

    public static bool IsEmbeddedAssetPack(string repoName, IReadOnlyList<string> blobPaths)
    {
        if (blobPaths.Count == 0)
        {
            return false;
        }

        var assetPackHits = blobPaths.Count(IsAssetPackPath);
        var csCount = blobPaths.Count(p =>
            p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));

        if (csCount == 0)
        {
            return false;
        }

        if (assetPackHits >= 3)
        {
            return true;
        }

        if (assetPackHits > 0 && assetPackHits * 3 >= csCount)
        {
            return true;
        }

        return AssetPackFileNames.Any(name =>
            blobPaths.Any(p => p.EndsWith(name, StringComparison.OrdinalIgnoreCase)));
    }

    public static bool IsUnityLearningRepository(string repoName, string? description = null)
    {
        var text = $"{repoName} {description ?? string.Empty}".ToLowerInvariant();

        if (LearningRepoNameTokens.Any(token => text.Contains(token, StringComparison.Ordinal)))
        {
            return true;
        }

        return false;
    }

    public static bool IsUnityToolkitRepository(string repoName, string manifestOrFramework)
    {
        var text = $"{repoName} {manifestOrFramework}".ToLowerInvariant();

        if (text.Contains("typed-scenes", StringComparison.Ordinal) ||
            text.Contains("typed_scenes", StringComparison.Ordinal))
        {
            return true;
        }

        if (repoName.Contains("amigos", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (repoName.Contains("architecture-examples", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return (text.Contains("plugins/", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("package/plugin", StringComparison.OrdinalIgnoreCase)) &&
               (text.Contains("editor scripts", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("editor/", StringComparison.OrdinalIgnoreCase) ||
                repoName.Contains("toolkit", StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasCompositionRootPattern(IReadOnlyList<string> blobPaths, string manifest) =>
        blobPaths.Any(p =>
            p.Contains("CompositeRoot", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("CompositionRoot", StringComparison.OrdinalIgnoreCase)) ||
        manifest.Contains("CompositeRoot", StringComparison.OrdinalIgnoreCase);

    public static double AdjustAuditRankScore(string repoName, int sizeKb, double baseScore)
    {
        var lower = repoName.ToLowerInvariant();

        if (QualityRepoNameTokens.Any(t => lower.Contains(t, StringComparison.Ordinal)))
        {
            baseScore += 45;
        }

        if (LowValueRepoNameTokens.Any(t => lower.Contains(t, StringComparison.Ordinal)))
        {
            baseScore -= 400;
        }

        if (sizeKb >= LargeRepoSizeKbThreshold)
        {
            baseScore -= 600;
        }
        else if (sizeKb >= 5_000)
        {
            baseScore -= 120;
        }

        return baseScore;
    }

    public static List<string> FilterKeyFilesForDisplay(IReadOnlyList<string> keyFiles, IReadOnlyList<string> blobPaths)
    {
        if (keyFiles.Count == 0)
        {
            return [];
        }

        return keyFiles
            .Where(f => !IsAssetPackPath(f) && !IsAssetPackPathByFileName(f))
            .Where(f => blobPaths.Count == 0 || blobPaths.Any(p =>
                p.EndsWith('/' + f, StringComparison.OrdinalIgnoreCase) ||
                p.Equals(f, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
    }

    private static bool IsAssetPackPath(string path)
    {
        if (AssetPackPathMarkers.Any(m => path.Contains(m, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return IsAssetPackPathByFileName(Path.GetFileName(path));
    }

    private static bool IsAssetPackPathByFileName(string fileName) =>
        fileName.StartsWith("ETFX", StringComparison.OrdinalIgnoreCase) ||
        AssetPackFileNames.Any(n => fileName.Equals(n, StringComparison.OrdinalIgnoreCase));
}
