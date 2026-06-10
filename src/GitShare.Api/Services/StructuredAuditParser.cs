using System.Text.Json;
using System.Text.RegularExpressions;
using GitShare.Api.Models;

namespace GitShare.Api.Services;

internal static partial class StructuredAuditParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static StructuredAuditResponse? TryParse(
        string? rawContent,
        AuditContentLocale locale = AuditContentLocale.Ru)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        var json = ExtractJsonPayload(rawContent);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var prepared = JsonPayloadNormalizer.Prepare(json);
        var unwrapped = UnwrapAuditRoot(prepared);

        var parsed = TryDeserialize(unwrapped, locale);
        if (parsed is not null)
        {
            return parsed;
        }

        var repaired = JsonPayloadNormalizer.TryRepairForDeserialize(unwrapped);
        if (repaired is not null)
        {
            parsed = TryDeserialize(UnwrapAuditRoot(repaired), locale);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        return null;
    }

    /// <summary>
    /// Модель иногда оборачивает аудит в корневое поле (например audit, data).
    /// </summary>
    private static string UnwrapAuditRoot(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("Projects", out _))
            {
                return json;
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return json;
            }

            foreach (var propertyName in new[] { "audit", "Audit", "data", "result", "response", "output" })
            {
                if (!root.TryGetProperty(propertyName, out var nested))
                {
                    continue;
                }

                if (nested.ValueKind == JsonValueKind.Object &&
                    nested.TryGetProperty("Projects", out _))
                {
                    return nested.GetRawText();
                }
            }
        }
        catch (JsonException)
        {
            /* keep original payload */
        }

        return json;
    }

    private static StructuredAuditResponse? TryDeserialize(string json, AuditContentLocale locale)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<StructuredAuditResponse>(json, JsonOptions);
            if (parsed is null || parsed.Projects is null || parsed.Projects.Count == 0)
            {
                return null;
            }

            ApplyLegacyFieldAliases(json, parsed);
            return StructuredAuditBuilder.Normalize(parsed, locale);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Переносит устаревшие ключи ответа (например GitCultureScore) в текущую схему.</summary>
    private static void ApplyLegacyFieldAliases(string json, StructuredAuditResponse parsed)
    {
        if (!string.IsNullOrWhiteSpace(parsed.GitFormatStandard))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("GitCultureScore", out var legacy))
            {
                return;
            }

            var raw = legacy.GetString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                parsed.GitFormatStandard = GitTelemetryAnalyzer.NormalizeFormatStandard(raw);
            }
        }
        catch (JsonException)
        {
            /* ignore */
        }
    }

    private static string ExtractJsonPayload(string content)
    {
        var trimmed = content.Trim();
        var fenced = JsonFenceRegex().Match(trimmed);
        if (fenced.Success)
        {
            return fenced.Groups[1].Value.Trim();
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return trimmed;
        }

        return trimmed[start..(end + 1)];
    }

    [GeneratedRegex(@"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase)]
    private static partial Regex JsonFenceRegex();
}
