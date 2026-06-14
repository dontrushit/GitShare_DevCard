using GitShare.Api.Models;

namespace GitShare.Api.Services;

internal static class RepositoryForensicsCompressor
{
    private static readonly HashSet<string> ArchitectureExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".tsx", ".xaml", ".json", ".c", ".h"
    };

    private static readonly string[] LazyCommitTokens =
    [
        "update",
        "fix",
        "first commit",
        "initial commit",
        "111",
        "test",
        "changes",
        "commit",
        "wip",
        "asdf",
        "tmp"
    ];

    public static string CompressTreeSnapshot(IEnumerable<string> filePaths)
    {
        var relevantFiles = filePaths
            .Where(path => ArchitectureExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(120)
            .ToList();

        if (relevantFiles.Count == 0)
        {
            return "No architecture-relevant files detected (.cs/.ts/.tsx/.xaml/.json).";
        }

        var directories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in relevantFiles)
        {
            var normalized = path.Replace('\\', '/');
            var directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(directory))
            {
                directories.Add(Path.GetFileName(normalized) ?? normalized);
                continue;
            }

            var segments = directory.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 1; i <= segments.Length; i++)
            {
                directories.Add(string.Join('/', segments.Take(i)) + "/");
            }
        }

        var layout = string.Join(", ", directories.Take(40));
        return $"Architectural files: {relevantFiles.Count}; layout: {layout}";
    }

    public static string CompressCommitSnapshot(IReadOnlyList<string> commitMessages)
    {
        if (commitMessages.Count == 0)
        {
            return "No commit history available.";
        }

        var recent = commitMessages
            .Take(5)
            .Select(message => message.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();

        if (recent.Count == 0)
        {
            return "Commit messages are empty or unreadable.";
        }

        var lazyCount = recent.Count(IsLazyCommitMessage);
        var lines = recent.Select((message, index) =>
        {
            var label = IsLazyCommitMessage(message) ? "LAZY" : "DISCIPLINED";
            var trimmed = message.Length <= 140 ? message : message[..140] + "…";
            return $"[{index + 1}] {label}: {trimmed}";
        });

        return $"Recent commits (lazy={lazyCount}/{recent.Count}):\n{string.Join("\n", lines)}";
    }

    public static bool IsLazyCommitMessage(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();

        if (normalized.Length <= 3)
        {
            return true;
        }

        if (LazyCommitTokens.Any(token => normalized == token || normalized.StartsWith($"{token} ", StringComparison.Ordinal)))
        {
            return true;
        }

        return !normalized.Contains(' ') && normalized.Length < 12;
    }
}

internal sealed record RepositoryForensics(
    string RepoName,
    string Readme,
    string TreeSnapshot,
    string CommitSnapshot,
    string TargetSignatureManifest,
    IReadOnlyList<string> VerifiedPros,
    IReadOnlyList<string> VerifiedCons,
    IReadOnlyList<KeyFileContentEntry> KeyFilesContent,
    string EvidenceDigestJson,
    StackEvidenceProfile StackProfile,
    bool IsVendorAssetPack = false,
    IReadOnlyList<string>? BlobPaths = null,
    int Stars = 0,
    CodeEvidenceFacts? Facts = null)
{
    public IReadOnlyList<string> BlobPaths { get; } = BlobPaths ?? [];
}
