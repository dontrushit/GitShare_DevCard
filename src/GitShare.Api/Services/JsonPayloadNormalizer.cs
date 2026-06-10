using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GitShare.Api.Services;

/// <summary>
/// Подготовка JSON-ответа модели к десериализации через <see cref="System.Text.Json"/>.
/// </summary>
internal static partial class JsonPayloadNormalizer
{
    public static string Prepare(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(json.Length);
        foreach (var ch in json.Trim())
        {
            builder.Append(ch switch
            {
                '\u201c' or '\u201d' => '"',
                '\u2018' or '\u2019' => '\'',
                '\uFEFF' or '\u200B' => ' ',
                _ => ch
            });
        }

        return TrailingCommaRegex().Replace(builder.ToString(), "$1");
    }

    public static string? TryRepairForDeserialize(string json)
    {
        var prepared = Prepare(json);
        if (string.IsNullOrWhiteSpace(prepared))
        {
            return null;
        }

        try
        {
            using var _ = JsonDocument.Parse(prepared);
            return prepared;
        }
        catch (JsonException)
        {
            return TryEscapeUnescapedQuotesInStrings(prepared);
        }
    }

    /// <summary>
    /// Экранирует неэкранированные кавычки внутри строк JSON (типичная ошибка в ответе модели).
    /// </summary>
    private static string? TryEscapeUnescapedQuotesInStrings(string json)
    {
        var output = new StringBuilder(json.Length + 16);
        var inString = false;
        var escaped = false;

        for (var i = 0; i < json.Length; i++)
        {
            var ch = json[i];

            if (!inString)
            {
                output.Append(ch);
                if (ch == '"')
                {
                    inString = true;
                }

                continue;
            }

            if (escaped)
            {
                output.Append(ch);
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                output.Append(ch);
                escaped = true;
                continue;
            }

            if (ch == '"')
            {
                var j = i + 1;
                while (j < json.Length && char.IsWhiteSpace(json[j]))
                {
                    j++;
                }

                if (j >= json.Length || json[j] is ',' or '}' or ']' or ':')
                {
                    output.Append(ch);
                    inString = false;
                }
                else
                {
                    output.Append('\\').Append(ch);
                }

                continue;
            }

            output.Append(ch);
        }

        var repaired = output.ToString();
        try
        {
            using var _ = JsonDocument.Parse(repaired);
            return repaired;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@",(\s*[}\]])", RegexOptions.Compiled)]
    private static partial Regex TrailingCommaRegex();
}
