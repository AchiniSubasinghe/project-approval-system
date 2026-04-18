using System.Security.Claims;
using project_approval_system.Services.Chatbot.Models;

namespace project_approval_system.Services.Chatbot;

public interface IChatbotService
{
    IAsyncEnumerable<ChatTurnEvent> RunTurnAsync(
        IList<ChatMessage> history,
        string userInput,
        ClaimsPrincipal user,
        CancellationToken ct);

    IAsyncEnumerable<ChatTurnEvent> ResumePendingActionAsync(
        IList<ChatMessage> history,
        string token,
        bool confirmed,
        ClaimsPrincipal user,
        CancellationToken ct);
}
