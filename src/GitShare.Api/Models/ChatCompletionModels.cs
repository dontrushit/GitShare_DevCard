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
