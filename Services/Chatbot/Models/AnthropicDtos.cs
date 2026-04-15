using System.Text.Json;
using System.Text.Json.Serialization;

namespace project_approval_system.Services.Chatbot.Models;

internal sealed class AnthropicMessagesRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("messages")]
    public List<AnthropicMessage> Messages { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<AnthropicTool>? Tools { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

internal sealed class AnthropicMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public List<AnthropicContentBlock> Content { get; set; } = new();
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AnthropicTextBlock), typeDiscriminator: "text")]
[JsonDerivedType(typeof(AnthropicToolUseBlock), typeDiscriminator: "tool_use")]
[JsonDerivedType(typeof(AnthropicToolResultBlock), typeDiscriminator: "tool_result")]
internal abstract class AnthropicContentBlock
{
}

internal sealed class AnthropicTextBlock : AnthropicContentBlock
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

internal sealed class AnthropicToolUseBlock : AnthropicContentBlock
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    public JsonElement Input { get; set; }
}

internal sealed class AnthropicToolResultBlock : AnthropicContentBlock
{
    [JsonPropertyName("tool_use_id")]
    public string ToolUseId { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("is_error")]
    public bool IsError { get; set; }
}

internal sealed class AnthropicTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("input_schema")]
    public JsonElement InputSchema { get; set; }
}

internal sealed class AnthropicStreamEvent
{
    public string Type { get; set; } = string.Empty;
    public int? Index { get; set; }
    public AnthropicContentBlock? ContentBlock { get; set; }
    public string? TextDelta { get; set; }
    public string? PartialJson { get; set; }
    public string? StopReason { get; set; }
    public string? ErrorMessage { get; set; }
}
