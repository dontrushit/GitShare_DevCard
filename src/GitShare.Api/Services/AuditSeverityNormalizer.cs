namespace GitShare.Api.Services;

internal static class AuditSeverityNormalizer
{
    public static string Normalize(string? severity) =>
        severity?.Trim() switch
        {
            "NONE" or "None" or "N/A" => "NONE",
            "CLEAN" or "Clean" => "CLEAN",
            "Critical" => "Critical",
            "Minor" => "Minor",
            "Warning" => "Warning",
            _ => "Warning"
        };

    public static bool IsNonProductionSeverity(string severity) =>
        severity.Equals("NONE", StringComparison.OrdinalIgnoreCase) ||
        severity.Equals("CLEAN", StringComparison.OrdinalIgnoreCase) ||
        severity.Equals("Minor", StringComparison.OrdinalIgnoreCase);
}
