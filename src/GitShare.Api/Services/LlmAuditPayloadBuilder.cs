using System.Text;
using System.Text.Json;
using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>
/// Сборка user-payload для LLM: глубина по 3 репозиториям, без шума портфеля.
/// </summary>
internal static class LlmAuditPayloadBuilder
{
    /// <summary>~3500–4000 tokens с запасом под system prompt.</summary>
    public const int DefaultMaxUserPayloadChars = 18_000;

    public const string UntrustedEvidenceOpenTag = "<<<UNTRUSTED_GITHUB_EVIDENCE>>>";
    public const string UntrustedEvidenceCloseTag = "<<</UNTRUSTED_GITHUB_EVIDENCE>>>";

    private const int MaxLlmRepositories = 3;
    private const int MaxReadmeChars = 250;
    private const int MaxManifestChars = 1_400;
    private const int MaxKeyFileChars = 2_200;
    private const int MaxKeyFilesPerRepo = 5;
    private const int MaxDigestChars = 1_600;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string Build(
        DevCardProfile profile,
        IReadOnlyList<RepositoryForensics> forensics,
        GitHubActivityTelemetry telemetry,
        int maxChars = DefaultMaxUserPayloadChars)
    {
        var payload = BuildCore(profile, forensics, aggressive: false);
        if (payload.Length <= maxChars)
        {
            return WrapUntrustedEvidence(payload);
        }

        payload = BuildCore(profile, forensics, aggressive: true);
        if (payload.Length <= maxChars)
        {
            return WrapUntrustedEvidence(payload);
        }

        return WrapUntrustedEvidence(TruncateWithNotice(payload, maxChars));
    }

    public static string WrapUntrustedEvidence(string payload) =>
        $"{UntrustedEvidenceOpenTag}\n{payload}\n{UntrustedEvidenceCloseTag}";

    private static string BuildCore(
        DevCardProfile profile,
        IReadOnlyList<RepositoryForensics> forensics,
        bool aggressive)
    {
        var readmeLimit = aggressive ? 0 : MaxReadmeChars;
        var manifestLimit = aggressive ? 700 : MaxManifestChars;
        var keyFileLimit = aggressive ? 900 : MaxKeyFileChars;
        var keyFilesCount = aggressive ? 2 : MaxKeyFilesPerRepo;
        var deepRepos = forensics.Take(MaxLlmRepositories).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("=== PORTFOLIO CONTEXT (minimal) ===");
        sb.AppendLine($"Username: {profile.Username}");
        sb.AppendLine($"TotalStars: {profile.TotalStars}");
        sb.AppendLine($"AuditedReposForLlm: {deepRepos.Count} of {forensics.Count} (deepest dive only)");
        sb.AppendLine();

        foreach (var evidence in deepRepos)
        {
            sb.AppendLine($"=== REPOSITORY FORENSICS: {evidence.RepoName} ===");
            sb.AppendLine($"Stars: {evidence.Stars}");

            if (readmeLimit > 0)
            {
                sb.AppendLine("--- README (excerpt) ---");
                sb.AppendLine(
                    string.IsNullOrWhiteSpace(evidence.Readme)
                        ? "(missing)"
                        : Truncate(evidence.Readme, readmeLimit));
            }

            sb.AppendLine("--- TREE SNAPSHOT ---");
            sb.AppendLine(evidence.TreeSnapshot);
            sb.AppendLine("--- TARGET FILE SIGNATURES ---");
            sb.AppendLine(
                Truncate(
                    string.IsNullOrWhiteSpace(evidence.TargetSignatureManifest)
                        ? "(none)"
                        : evidence.TargetSignatureManifest,
                    manifestLimit));
            sb.AppendLine("--- SERVER VERIFIED FACTS (authoritative) ---");
            sb.AppendLine(
                string.IsNullOrWhiteSpace(evidence.EvidenceDigestJson)
                    ? "{}"
                    : Truncate(evidence.EvidenceDigestJson, MaxDigestChars));
            sb.AppendLine($"StackProfile: {evidence.StackProfile}");
            sb.AppendLine("--- KEY FILES CONTENT ---");
            sb.AppendLine(
                FormatKeyFilesContent(evidence.KeyFilesContent, keyFilesCount, keyFileLimit));
        }

        if (forensics.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("No repository forensics available.");
        }

        return sb.ToString();
    }

    private static string FormatKeyFilesContent(
        IReadOnlyList<KeyFileContentEntry> entries,
        int maxFiles,
        int maxCharsPerFile)
    {
        if (entries is not { Count: > 0 })
        {
            return "KeyFilesContent: []";
        }

        var payload = entries
            .Take(maxFiles)
            .Select(e => new
            {
                e.FileName,
                Content = Truncate(e.Content, maxCharsPerFile)
            });

        return "KeyFilesContent: " + JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string Truncate(string? text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text) || maxChars <= 0)
        {
            return string.Empty;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= maxChars ? trimmed : trimmed[..maxChars] + "…";
    }

    private static string TruncateWithNotice(string payload, int maxChars)
    {
        if (payload.Length <= maxChars)
        {
            return payload;
        }

        const string notice = "\n[Payload truncated to fit model input limit]\n";
        var budget = Math.Max(0, maxChars - notice.Length);
        return payload[..budget] + notice;
    }
}
