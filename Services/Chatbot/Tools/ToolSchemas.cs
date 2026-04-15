using System.Text.Json;

namespace project_approval_system.Services.Chatbot.Tools;

internal static class ToolSchemas
{
    public static readonly JsonSerializerOptions CompactJson = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static JsonElement Parse(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    public const string Empty = """{"type":"object","properties":{},"additionalProperties":false}""";

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, CompactJson);

    public static int GetIntProperty(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var value))
        {
            throw new ArgumentException($"Missing required argument '{name}'.");
        }
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => throw new ArgumentException($"Argument '{name}' must be an integer."),
        };
    }

    public static int? GetOptionalIntProperty(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value))
        {
            return null;
        }
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    public static string? GetOptionalStringProperty(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value))
        {
            return null;
        }
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
