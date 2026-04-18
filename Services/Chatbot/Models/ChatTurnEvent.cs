namespace project_approval_system.Services.Chatbot.Models;

public abstract record ChatTurnEvent;

public sealed record TextDeltaEvent(string Delta) : ChatTurnEvent;

public sealed record AssistantMessageCompletedEvent(ChatMessage Message) : ChatTurnEvent;

public sealed record ToolCallStartedEvent(string ToolUseId, string ToolName) : ChatTurnEvent;

public sealed record ToolCallFinishedEvent(string ToolUseId, string ToolName, int ResultCharCount, bool IsError) : ChatTurnEvent;

public sealed record TurnCompletedEvent(string StopReason) : ChatTurnEvent;

public sealed record TurnErrorEvent(string Message) : ChatTurnEvent;

public sealed record ActionConfirmationRequiredEvent(
    string Token,
    string ToolUseId,
    string ToolName,
    string Summary,
    string ParamsJson) : ChatTurnEvent;

public sealed record ActionCommittedEvent(
    string Token,
    bool Confirmed,
    bool Succeeded,
    string Message) : ChatTurnEvent;
