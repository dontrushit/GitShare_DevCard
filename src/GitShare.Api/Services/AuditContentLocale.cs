namespace GitShare.Api.Services;

/// <summary>Язык текстов аудита (LLM + rule-based fallback), не язык UI-лейблов.</summary>
public enum AuditContentLocale
{
    Ru,
    En
}

public static class AuditContentLocaleParser
{
    public static AuditContentLocale Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AuditContentLocale.Ru;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "en" or "en-us" or "en-gb" ? AuditContentLocale.En : AuditContentLocale.Ru;
    }

    public static string ToCode(AuditContentLocale locale) =>
        locale == AuditContentLocale.En ? "en" : "ru";
}
