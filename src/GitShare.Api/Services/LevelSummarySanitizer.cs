using System.Text.RegularExpressions;

namespace GitShare.Api.Services;

internal static partial class LevelSummarySanitizer
{
    private const int MaxChars = 720;

    public static string Normalize(string? text, AuditContentLocale locale, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        var trimmed = CollapseWhitespace(text.Trim());
        trimmed = PromptInjectionGuard.SanitizeNarrative(trimmed, locale);

        if (PromptInjectionGuard.ContainsInjectionMarker(text) ||
            !AuditNarrativeValidator.IsValidNarrative(trimmed, locale) ||
            AuditNarrativeValidator.UsesInstructionalTone(trimmed) ||
            AuditTextSanitizer.ContainsForbiddenLanguage(trimmed))
        {
            return fallback;
        }

        var sentences = SplitSentences(trimmed);
        if (sentences.Count < 2)
        {
            return fallback;
        }

        if (sentences.Count > 3)
        {
            trimmed = string.Join(' ', sentences.Take(3));
        }

        if (trimmed.Length > MaxChars)
        {
            trimmed = trimmed[..MaxChars].TrimEnd() + "…";
        }

        return trimmed;
    }

    private static string CollapseWhitespace(string text) =>
        WhitespaceRegex().Replace(text, " ");

    private static List<string> SplitSentences(string text)
    {
        var parts = SentenceSplitRegex().Split(text)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        return parts.Count > 0 ? parts : [text];
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceSplitRegex();
}
