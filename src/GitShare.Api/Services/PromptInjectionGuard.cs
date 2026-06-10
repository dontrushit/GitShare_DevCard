namespace GitShare.Api.Services;

/// <summary>Пост-валидация LLM-нарратива на маркеры prompt injection.</summary>
internal static class PromptInjectionGuard
{
    public const string BlockedMessageRu =
        "[Текст отклонён фильтром безопасности: обнаружены подозрительные фрагменты во входных данных]";

    public const string BlockedMessageEn =
        "[Text rejected by the security filter: suspicious fragments were detected in the input data]";

    private static readonly string[] InjectionMarkers =
    [
        "ignore previous instructions",
        "ignore all previous",
        "ignore prior instructions",
        "disregard previous",
        "forget previous instructions",
        "system prompt",
        "you are now",
        "new instructions:",
        "override instructions",
        "забудь инструкции",
        "игнорируй предыдущ",
        "игнорируй прошлы",
        "системный промпт",
        "новые инструкции",
    ];

    public static bool ContainsInjectionMarker(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim();
        foreach (var marker in InjectionMarkers)
        {
            if (normalized.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string SanitizeNarrative(string? text, AuditContentLocale locale)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text ?? string.Empty;
        }

        return ContainsInjectionMarker(text)
            ? BlockedMessage(locale)
            : text.Trim();
    }

    public static string BlockedMessage(AuditContentLocale locale) =>
        locale == AuditContentLocale.En ? BlockedMessageEn : BlockedMessageRu;
}
