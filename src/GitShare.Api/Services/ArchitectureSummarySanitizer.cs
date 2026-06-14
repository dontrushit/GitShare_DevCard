namespace GitShare.Api.Services;

/// <summary>
/// Отсекает рекламные формулировки LLM; предпочитает evidence-based резюме сервера.
/// </summary>
internal static class ArchitectureSummarySanitizer
{
    private static readonly string[] MarketingMarkersRu =
    [
        "зрелая", "зрелый", "масштабируем", "полноценн", "современн",
        "высококачеств", "отличн", "идеальн", "best practice", "best-practice",
        "enterprise-grade", "production-ready", "высокий уровень",
        "улучшает", "стабильност", "надёжн", "качественн", "соответствует современным"
    ];

    private static readonly string[] MarketingMarkersEn =
    [
        "mature", "scalable", "full-fledged", "modern stack", "high quality",
        "excellent", "ideal", "best practice", "enterprise-grade", "production-ready",
        "meets modern", "well-architected", "robust architecture"
    ];

    public static string PickSummary(
        string? llmSummary,
        string evidenceSummary,
        CodeEvidenceFacts? facts,
        AuditContentLocale locale)
    {
        var evidence = evidenceSummary?.Trim() ?? string.Empty;
        var llm = llmSummary?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(llm))
        {
            return evidence;
        }

        if (!AuditNarrativeValidator.IsValidNarrative(llm, locale) || llm.Length < 40)
        {
            return evidence;
        }

        if (IsMarketingHype(llm, locale) || IsOutcomeClaim(llm, locale))
        {
            return evidence;
        }

        if (IsChecklistParaphrase(llm) || IsHostingChecklist(llm))
        {
            return evidence;
        }

        return llm;
    }

    public static bool IsMarketingHype(string text, AuditContentLocale locale)
    {
        var markers = locale == AuditContentLocale.En ? MarketingMarkersEn : MarketingMarkersRu;
        var lower = text.ToLowerInvariant();
        return markers.Any(m => lower.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsOutcomeClaim(string text, AuditContentLocale locale)
    {
        var lower = text.ToLowerInvariant();
        if (locale == AuditContentLocale.En)
        {
            return lower.Contains("improves ") || lower.Contains("ensures ") ||
                   lower.Contains("which improves") || lower.Contains("enhances ");
        }

        return lower.Contains("улучшает ") || lower.Contains("повышает ") ||
               lower.Contains("обеспечивает стабиль") || lower.Contains("что улучшает");
    }

    private static bool IsChecklistParaphrase(string text)
    {
        var lower = text.ToLowerInvariant();
        var hits = 0;
        if (lower.Contains("async")) hits++;
        if (lower.Contains("di") || lower.Contains("внедрен")) hits++;
        if (lower.Contains("services") || lower.Contains("сервис")) hits++;
        if (lower.Contains("repository") || lower.Contains("репозитор")) hits++;
        return hits >= 3;
    }

    private static bool IsHostingChecklist(string text)
    {
        var lower = text.ToLowerInvariant();
        return (lower.Contains("program.cs") || lower.Contains("startup")) &&
               (lower.Contains("async") || lower.Contains("try/catch") || lower.Contains("обработк"));
    }
}
