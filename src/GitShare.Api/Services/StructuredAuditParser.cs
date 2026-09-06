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
            ApplyKeyRisksFromRawPayload(json, parsed);
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

    /// <summary>
    /// KeyRisks помечен [JsonIgnore], чтобы не уезжать клиенту, — десериализатор его тоже пропускает.
    /// Поэтому риски достаём из сырого JSON и сопоставляем с проектами по RepoName.
    /// </summary>
    private static void ApplyKeyRisksFromRawPayload(string json, StructuredAuditResponse parsed)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!TryGetPropertyIgnoreCase(document.RootElement, "Projects", out var projects) ||
                projects.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var parsedByRepo = parsed.Projects
                .Where(p => !string.IsNullOrWhiteSpace(p.RepoName))
                .GroupBy(p => p.RepoName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var rawProject in projects.EnumerateArray())
            {
                if (!TryGetPropertyIgnoreCase(rawProject, "RepoName", out var rawName) ||
                    rawName.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var repoName = rawName.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(repoName) ||
                    !parsedByRepo.TryGetValue(repoName, out var target) ||
                    !TryGetPropertyIgnoreCase(rawProject, "KeyRisks", out var rawRisks))
                {
                    continue;
                }

                var risks = ExtractStringList(rawRisks);
                if (risks.Count > 0)
                {
                    target.KeyRisks = risks;
                }
            }
        }
        catch (JsonException)
        {
            /* KeyRisks необязательны: при битом JSON остаются пустыми */
        }
    }

    private static List<string> ExtractStringList(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var single = element.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(single) ? [] : [single];
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToList();
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
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
