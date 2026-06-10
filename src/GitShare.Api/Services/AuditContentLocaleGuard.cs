using GitShare.Api.Models;

namespace GitShare.Api.Services;

/// <summary>Проверяет, что текст аудита соответствует запрошенной локали (для инвалидации устаревшего кэша).</summary>
internal static class AuditContentLocaleGuard
{
    public static bool ProfileMatchesRequestedLocale(DevCardProfile profile, string localeCode)
    {
        var cached = string.IsNullOrWhiteSpace(profile.ContentLocale)
            ? "ru"
            : profile.ContentLocale.Trim().ToLowerInvariant();

        if (!string.Equals(cached, localeCode, StringComparison.Ordinal))
        {
            return false;
        }

        if (localeCode == "en" && AuditContainsCyrillic(profile.AuditData))
        {
            return false;
        }

        if (localeCode == "ru" && AuditIsPredominantlyEnglish(profile.AuditData))
        {
            return false;
        }

        return true;
    }

    public static bool AuditIsPredominantlyEnglish(StructuredAuditResponse? audit)
    {
        if (audit is null)
        {
            return false;
        }

        var combined = CollectNarrativeText(audit);
        if (combined.Length < 48)
        {
            return false;
        }

        var cyrillic = CountCyrillic(combined);
        if (cyrillic >= 8)
        {
            return false;
        }

        var latinLetters = combined.Count(static c => char.IsLetter(c) && c is (< '\u0400' or > '\u04FF'));
        return latinLetters >= 40;
    }

    private static string CollectNarrativeText(StructuredAuditResponse audit)
    {
        var chunks = new List<string>();
        if (!string.IsNullOrWhiteSpace(audit.CoreEngineeringFocus))
        {
            chunks.Add(audit.CoreEngineeringFocus);
        }

        foreach (var project in audit.Projects ?? [])
        {
            chunks.Add(project.TechnicalDebt);
            chunks.Add(project.InterviewTrapQuestion);
            chunks.AddRange(project.Pros ?? []);
            chunks.AddRange(project.Cons ?? []);
        }

        return string.Join(' ', chunks.Where(c => !string.IsNullOrWhiteSpace(c)));
    }

    public static bool AuditContainsCyrillic(StructuredAuditResponse? audit)
    {
        if (audit is null)
        {
            return false;
        }

        return CountCyrillic(CollectNarrativeText(audit)) >= 8;
    }

    private static int CountCyrillic(string text) =>
        text.Count(static c => c is >= '\u0400' and <= '\u04FF');
}
