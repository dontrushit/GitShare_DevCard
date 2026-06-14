namespace GitShare.Api.Services;

/// <summary>
/// Эвристики для OSS CLI/DevOps-репозиториев (Go marketplace, bootstrap tools и т.п.).
/// </summary>
internal static class OssRepositoryHeuristics
{
    private static readonly string[] FlagshipNameTokens =
    [
        "k3sup",
        "arkade",
        "openfaas",
        "inlets",
        "derek",
        "faas",
        "kubetrim"
    ];

    public static bool IsOssFlagshipRepository(string repoName, int stars = 0) =>
        stars >= 1_000 ||
        FlagshipNameTokens.Any(token =>
            repoName.Contains(token, StringComparison.OrdinalIgnoreCase));

    public static bool IsGoCliManifest(string manifest) =>
        manifest.Contains("go.mod", StringComparison.OrdinalIgnoreCase);

    public static bool IsGoCliAuditContext(string repoName, string manifest) =>
        IsGoCliManifest(manifest) || IsOssFlagshipRepository(repoName);

    public static bool IsKnownDevOpsTool(string repoName) =>
        FlagshipNameTokens.Any(token =>
            repoName.Equals(token, StringComparison.OrdinalIgnoreCase) ||
            repoName.Contains(token, StringComparison.OrdinalIgnoreCase));
}
