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
}
