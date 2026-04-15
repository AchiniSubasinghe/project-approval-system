using System.Security.Claims;
using System.Text.Json;
using project_approval_system.Data;
using project_approval_system.Services.Chatbot.Models;
using project_approval_system.Services.Chatbot.Tools;

namespace project_approval_system.Services.Chatbot;

internal sealed class ChatToolRegistry(
    StudentTools studentTools,
    SupervisorTools supervisorTools,
    ModuleLeaderTools moduleLeaderTools) : IChatToolRegistry
{
    public IReadOnlyList<ToolDefinition> ForUser(ClaimsPrincipal user)
    {
        var list = new List<ToolDefinition>();
        if (user.IsInRole(Roles.Student))
        {
            list.AddRange(studentTools.Definitions);
        }
        if (user.IsInRole(Roles.Supervisor))
        {
            list.AddRange(supervisorTools.Definitions);
        }
        if (user.IsInRole(Roles.ModuleLeader))
        {
            list.AddRange(moduleLeaderTools.Definitions);
        }
        return list;
    }

    public Task<string> ExecuteAsync(string toolName, JsonElement args, ChatToolContext ctx, CancellationToken ct)
    {
        return ctx.Role switch
        {
            Roles.Student when studentTools.Definitions.Any(t => t.Name == toolName)
                => studentTools.ExecuteAsync(toolName, args, ctx, ct),
            Roles.Supervisor when supervisorTools.Definitions.Any(t => t.Name == toolName)
                => supervisorTools.ExecuteAsync(toolName, args, ctx, ct),
            Roles.ModuleLeader when moduleLeaderTools.Definitions.Any(t => t.Name == toolName)
                => moduleLeaderTools.ExecuteAsync(toolName, args, ctx, ct),
            _ => throw new UnauthorizedAccessException(
                $"Tool '{toolName}' is not available to role '{ctx.Role}'."),
        };
    }
}
