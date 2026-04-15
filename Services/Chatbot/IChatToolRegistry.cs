using System.Security.Claims;
using System.Text.Json;
using project_approval_system.Services.Chatbot.Models;

namespace project_approval_system.Services.Chatbot;

public interface IChatToolRegistry
{
    IReadOnlyList<ToolDefinition> ForUser(ClaimsPrincipal user);

    Task<string> ExecuteAsync(string toolName, JsonElement args, ChatToolContext ctx, CancellationToken ct);
}
