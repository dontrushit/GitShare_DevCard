using System.Text;
using System.Text.Json;
using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>
/// Сборка user-payload для LLM с жёстким лимитом символов (GitHub Models gpt-4o ≈ 8000 tokens на запрос).
/// </summary>
internal static class LlmAuditPayloadBuilder
{
    /// <summary>~3500–4000 tokens с запасом под system prompt.</summary>
    public const int DefaultMaxUserPayloadChars = 14_000;

    public const string UntrustedEvidenceOpenTag = "<<<UNTRUSTED_GITHUB_EVIDENCE>>>";
    public const string UntrustedEvidenceCloseTag = "<<</UNTRUSTED_GITHUB_EVIDENCE>>>";

    private const int MaxReadmeChars = 400;
    private const int MaxManifestChars = 1_200;
    private const int MaxKeyFileChars = 1_200;
    private const int MaxKeyFilesPerRepo = 2;
    private const int MaxCommitMessages = 10;
    private const int MaxCommitMessageChars = 120;
    private const int MaxBioChars = 200;

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
        var payload = BuildCore(profile, forensics, telemetry, aggressive: false);
        if (payload.Length <= maxChars)
        {
            return WrapUntrustedEvidence(payload);
        }

        payload = BuildCore(profile, forensics, telemetry, aggressive: true);
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
        GitHubActivityTelemetry telemetry,
        bool aggressive)
    {
        var readmeLimit = aggressive ? 0 : MaxReadmeChars;
        var manifestLimit = aggressive ? 600 : MaxManifestChars;
        var keyFileLimit = aggressive ? 600 : MaxKeyFileChars;
        var keyFilesCount = aggressive ? 1 : MaxKeyFilesPerRepo;
        var repoLimit = aggressive ? 3 : forensics.Count;

        var languages = profile.LanguageStack.Count == 0
            ? "none detected"
            : string.Join(", ", profile.LanguageStack.Select(l => $"{l.Language} {l.Percentage}%"));

        var rhythm = profile.CommitRhythm.Count == 0
            ? "no activity windows detected"
            : string.Join(", ", profile.CommitRhythm.Take(12).Select(h => $"{h.Hour:00}:00={h.CommitCount}"));

        var sb = new StringBuilder();
        sb.AppendLine("=== GITHUB METRICS ===");
        sb.AppendLine($"Username: {profile.Username}");
        sb.AppendLine($"Bio: {Truncate(profile.Bio, MaxBioChars)}");
        sb.AppendLine($"Location: {profile.Location}");
        sb.AppendLine($"PublicRepos: {profile.PublicRepos}");
        sb.AppendLine($"TotalStars: {profile.TotalStars}");
        sb.AppendLine($"OwnRepositories: {profile.OwnRepositoryCount}");
        sb.AppendLine($"LanguageStack: {languages}");
        sb.AppendLine($"CommitRhythm (hour=events): {rhythm}");
        sb.AppendLine();
        sb.AppendLine(FormatTelemetryCompact(telemetry));
        sb.AppendLine();
        sb.AppendLine("=== TOP REPOSITORIES (metadata) ===");

        foreach (var repo in profile.TopRepositories)
        {
            var desc = Truncate(repo.Description, 80);
            sb.AppendLine($"- {repo.Name} | Stars: {repo.Stars} | Lang: {repo.Language} | {desc}");
        }

        foreach (var evidence in forensics.Take(repoLimit))
        {
            sb.AppendLine();
            sb.AppendLine($"=== REPOSITORY FORENSICS: {evidence.RepoName} ===");

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
            sb.AppendLine("--- COMMIT SNAPSHOT ---");
            sb.AppendLine(evidence.CommitSnapshot);
            sb.AppendLine("--- TARGET FILE SIGNATURES ---");
            sb.AppendLine(
                Truncate(
                    string.IsNullOrWhiteSpace(evidence.TargetSignatureManifest)
                        ? "(none)"
                        : evidence.TargetSignatureManifest,
                    manifestLimit));
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

    private static string FormatTelemetryCompact(GitHubActivityTelemetry telemetry)
    {
        var total = telemetry.CommitsInWorkingHours + telemetry.CommitsInOffHours;
        var workingPercent = total == 0
            ? 0
            : Math.Round(telemetry.CommitsInWorkingHours * 100.0 / total, 1);

        var sb = new StringBuilder();
        sb.AppendLine("=== COMMIT ACTIVITY (compact) ===");
        sb.AppendLine($"WorkingHoursPercent: {workingPercent}%");
        sb.AppendLine($"RecentCommitMessages (max {MaxCommitMessages}):");

        var messages = telemetry.RecentCommitMessages
            .Take(MaxCommitMessages)
            .Select(m => Truncate(m.Split('\n')[0].Trim(), MaxCommitMessageChars))
            .Where(m => m.Length > 0);

        foreach (var message in messages)
        {
            sb.AppendLine($"- {message}");
        }

        if (!telemetry.RecentCommitMessages.Any())
        {
            sb.AppendLine("- (none)");
        }

        if (telemetry.ExternalPullRequests.Count > 0)
        {
            sb.AppendLine("External PR repos:");
            foreach (var repo in telemetry.ExternalPullRequests.Take(5))
            {
                sb.AppendLine($"- {repo}");
            }
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
