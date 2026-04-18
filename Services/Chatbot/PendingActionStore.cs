using Microsoft.Extensions.Caching.Memory;

namespace project_approval_system.Services.Chatbot;

internal sealed record PendingAction(
    string Token,
    string UserId,
    string ToolUseId,
    string ToolName,
    string ArgsJson,
    DateTime CreatedAt);

internal interface IPendingActionStore
{
    string Enqueue(string userId, string toolUseId, string toolName, string argsJson);

    PendingAction? Consume(string token, string userId);
}

internal sealed class PendingActionStore(IMemoryCache cache) : IPendingActionStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public string Enqueue(string userId, string toolUseId, string toolName, string argsJson)
    {
        var token = Guid.NewGuid().ToString("N");
        var entry = new PendingAction(token, userId, toolUseId, toolName, argsJson, DateTime.UtcNow);
        cache.Set(Key(token), entry, Ttl);
        return token;
    }

    public PendingAction? Consume(string token, string userId)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var key = Key(token);
        if (!cache.TryGetValue<PendingAction>(key, out var entry) || entry is null) return null;
        if (!string.Equals(entry.UserId, userId, StringComparison.Ordinal)) return null;
        cache.Remove(key);
        return entry;
    }

    private static string Key(string token) => $"pending-action:{token}";
}
