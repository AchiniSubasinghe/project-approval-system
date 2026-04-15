using System.Text.Json;

namespace project_approval_system.Services.Chatbot.Models;

public sealed record ToolDefinition(
    string Name,
    string Description,
    JsonElement InputSchema);

public readonly record struct ChatToolContext(
    string UserId,
    string DisplayName,
    string Role);
