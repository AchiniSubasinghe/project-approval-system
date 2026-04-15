using System.Text.Json;

namespace project_approval_system.Services.Chatbot.Models;

public enum ChatRole
{
    User,
    Assistant,
}

public sealed class ChatMessage
{
    public ChatRole Role { get; init; }

    public List<ChatContentBlock> Content { get; init; } = new();

    public static ChatMessage UserText(string text) => new()
    {
        Role = ChatRole.User,
        Content = { new ChatContentBlock { Type = ChatContentType.Text, Text = text } },
    };
}

public enum ChatContentType
{
    Text,
    ToolUse,
    ToolResult,
}

public sealed class ChatContentBlock
{
    public ChatContentType Type { get; set; }

    public string? Text { get; set; }

    public string? ToolUseId { get; set; }

    public string? ToolName { get; set; }

    public JsonElement? ToolInput { get; set; }

    public string? ToolResultContent { get; set; }

    public bool ToolResultIsError { get; set; }
}
