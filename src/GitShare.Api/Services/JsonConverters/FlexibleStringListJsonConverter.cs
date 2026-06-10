using System.Text.Json;
using System.Text.Json.Serialization;

namespace GitShare.Api.Services.JsonConverters;

/// <summary>
/// Модель иногда возвращает строку вместо JSON-массива (например KeyFiles).
/// </summary>
internal sealed class FlexibleStringListJsonConverter : JsonConverter<List<string>>
{
    public override List<string> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => [],
            JsonTokenType.String => SplitList(reader.GetString()),
            JsonTokenType.StartArray => ReadArray(ref reader, options),
            _ => throw new JsonException($"Expected string or array for list, got {reader.TokenType}.")
        };
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }

    private static List<string> ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var items = new List<string>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return items;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var part = reader.GetString()?.Trim();
                if (!string.IsNullOrEmpty(part))
                {
                    items.Add(part);
                }
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                reader.Skip();
            }
        }

        return items;
    }

    private static List<string> SplitList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
