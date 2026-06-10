namespace GitShare.Api.Services;

/// <summary>
/// Фильтр нарратива аудита по языку контента (RU/EN).
/// </summary>
internal static class AuditNarrativeValidator
{
    public static bool IsValidNarrative(string? text, AuditContentLocale locale) =>
        locale == AuditContentLocale.En ? IsValidEnglishNarrative(text) : IsValidRussianNarrative(text);

    private static readonly string[] EnglishBoilerplateMarkers =
    [
        "no significant",
        "not applicable",
        "the repository",
        "this repository",
        "appears to be",
        "appears to implement",
        "architectural issues detected",
        "focused on providing",
        "focused on automated",
        "the structure is",
        "without dedicated",
        "the project",
        "makes it difficult",
        "violating the",
        "which makes"
    ];

    public static bool IsValidRussianNarrative(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (AuditTextSanitizer.ContainsForbiddenLanguage(trimmed))
        {
            return false;
        }

        if (IsEnglishBoilerplate(trimmed))
        {
            return false;
        }

        var cyrillicCount = trimmed.Count(static c => c is >= '\u0400' and <= '\u04FF');
        if (cyrillicCount == 0 && trimmed.Length > 24)
        {
            return false;
        }

        if (cyrillicCount < 10 && trimmed.Length > 80)
        {
            return false;
        }

        return true;
    }

    public static bool IsValidEnglishNarrative(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (AuditTextSanitizer.ContainsForbiddenLanguage(trimmed))
        {
            return false;
        }

        var cyrillicCount = trimmed.Count(static c => c is >= '\u0400' and <= '\u04FF');
        if (cyrillicCount > 0)
        {
            return false;
        }

        if (UsesInstructionalTone(trimmed))
        {
            return false;
        }

        return trimmed.Length >= 8;
    }

    public static bool IsEnglishBoilerplate(string text)
    {
        var lower = text.ToLowerInvariant();
        return EnglishBoilerplateMarkers.Any(marker => lower.Contains(marker, StringComparison.Ordinal));
    }

    public static bool IsGenericUtilityInterviewQuestion(string text)
    {
        var lower = text.ToLowerInvariant();
        return lower.Contains("разделяете конфигурацию, секреты", StringComparison.Ordinal) ||
               lower.Contains("модульность в cli", StringComparison.Ordinal) ||
               lower.Contains("организацию модульности", StringComparison.Ordinal) ||
               lower.Contains("cli-приложен", StringComparison.Ordinal) && lower.Contains("go", StringComparison.Ordinal);
    }

    private static readonly string[] InstructionalToneMarkers =
    [
        "оценивайте",
        "смотрите",
        "обратите внимание",
        "не ищите",
        "не оценивайте",
        "рекомендуется",
        "следует оценить",
        "нужен code review",
        "нужно оценить"
    ];

    public static bool UsesInstructionalTone(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lower = text.Trim().ToLowerInvariant();
        if (InstructionalToneMarkers.Any(marker => lower.Contains(marker, StringComparison.Ordinal)))
        {
            return true;
        }

        return EnglishInstructionalToneMarkers.Any(marker => lower.Contains(marker, StringComparison.Ordinal));
    }

    private static readonly string[] EnglishInstructionalToneMarkers =
    [
        "you should",
        "you must",
        "consider ",
        "pay attention",
        "note that",
        "make sure",
        "be sure to",
        "it is recommended",
        "needs code review"
    ];
}
