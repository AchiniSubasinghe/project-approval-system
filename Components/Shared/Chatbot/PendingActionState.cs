namespace project_approval_system.Components.Shared.Chatbot;

public sealed class PendingActionState
{
    public required string Token { get; init; }
    public required string ToolUseId { get; init; }
    public required string ToolName { get; init; }
    public required string Summary { get; init; }
    public PendingActionStatus Status { get; set; } = PendingActionStatus.AwaitingConfirmation;
    public string? ResultMessage { get; set; }
}

public enum PendingActionStatus
{
    AwaitingConfirmation,
    Executing,
    Confirmed,
    Canceled,
}
