using System.Text.Json;
using System.Text.Json.Serialization;

namespace GitShare.Api.Models;

internal sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatCompletionMessage> Messages { get; set; } = [];

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 2048;

    [JsonPropertyName("response_format")]
    public ChatCompletionResponseFormat? ResponseFormat { get; set; }
}

internal sealed class ChatCompletionResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "json_object";

    [JsonPropertyName("json_schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChatCompletionJsonSchema? JsonSchema { get; set; }

    /// <summary>
    /// OpenAI / GitHub Models: json_object. Anthropic совместимый слой принимает только json_schema.
    /// </summary>
    public static ChatCompletionResponseFormat ForAudit(string baseUrl)
    {
        if (baseUrl.Contains("anthropic.com", StringComparison.OrdinalIgnoreCase))
        {
            return new ChatCompletionResponseFormat
            {
                Type = "json_schema",
                JsonSchema = new ChatCompletionJsonSchema
                {
                    Name = "structured_audit",
                    Schema = AuditJsonSchema.Root
                }
            };
        }

        return new ChatCompletionResponseFormat { Type = "json_object" };
    }
}

internal sealed class ChatCompletionJsonSchema
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "structured_audit";

    [JsonPropertyName("strict")]
    public bool Strict { get; set; } = true;

    [JsonPropertyName("schema")]
    public JsonElement Schema { get; set; }
}

internal static class AuditJsonSchema
{
    private static readonly JsonDocument Document = JsonDocument.Parse(
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "Projects": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "RepoName": { "type": "string" },
                  "ProjectClass": { "type": "string" },
                  "Framework": { "type": "string" },
                  "LayoutType": { "type": "string" },
                  "KeyFiles": { "type": "array", "items": { "type": "string" } },
                  "ArchitectureSummary": { "type": "string" },
                  "TechnicalDebt": { "type": "string" },
                  "DebtSeverity": { "type": "string" },
                  "KeyRisks": { "type": "array", "items": { "type": "string" } },
                  "InterviewTrapQuestion": { "type": "string" }
                },
                "required": [
                  "RepoName", "ProjectClass", "Framework", "LayoutType", "KeyFiles",
                  "ArchitectureSummary", "TechnicalDebt", "DebtSeverity", "KeyRisks",
                  "InterviewTrapQuestion"
                ]
              }
            },
            "CoreEngineeringFocus": { "type": "string" }
          },
          "required": ["Projects", "CoreEngineeringFocus"]
        }
        """);

    public static JsonElement Root => Document.RootElement;
}

internal sealed class ChatCompletionMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

internal sealed class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<ChatCompletionChoice>? Choices { get; set; }
}

internal sealed class ChatCompletionChoice
{
    [JsonPropertyName("message")]
    public ChatCompletionMessage? Message { get; set; }
}
