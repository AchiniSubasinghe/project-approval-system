using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using project_approval_system.Services.Chatbot.Models;

namespace project_approval_system.Services.Chatbot;

internal sealed class AnthropicClient
{
    private const string ApiVersion = "2023-06-01";
    private const string KeyConfigPath = "Anthropic:ApiKey";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly AnthropicOptions _options;
    private readonly string _apiKey;

    public AnthropicClient(HttpClient http, IOptions<AnthropicOptions> options, IConfiguration config)
    {
        _http = http;
        _options = options.Value;
        _apiKey = config[KeyConfigPath]
            ?? throw new InvalidOperationException(
                $"Anthropic API key missing. Set it via user secrets: dotnet user-secrets set \"{KeyConfigPath}\" \"sk-ant-...\"");

        _http.BaseAddress = new Uri(_options.BaseUrl);
        _http.Timeout = TimeSpan.FromMinutes(2);
    }

    public async IAsyncEnumerable<AnthropicStreamItem> StreamAsync(
        AnthropicMessagesRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        request.Stream = true;

        using var http = new HttpRequestMessage(HttpMethod.Post, "/v1/messages");
        http.Headers.Add("x-api-key", _apiKey);
        http.Headers.Add("anthropic-version", ApiVersion);
        http.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var json = JsonSerializer.Serialize(request, SerializerOptions);
        http.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(http, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Anthropic API error {(int)response.StatusCode}: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var contentBlocks = new Dictionary<int, ContentBlockBuilder>();
        string? currentEvent = null;
        var dataBuffer = new StringBuilder();

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                if (currentEvent is not null && dataBuffer.Length > 0)
                {
                    foreach (var item in ProcessEvent(currentEvent, dataBuffer.ToString(), contentBlocks))
                    {
                        yield return item;
                    }
                }
                currentEvent = null;
                dataBuffer.Clear();
                continue;
            }

            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                currentEvent = line[7..].Trim();
            }
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                if (dataBuffer.Length > 0)
                {
                    dataBuffer.Append('\n');
                }
                dataBuffer.Append(line[6..]);
            }
        }

        var finalBlocks = contentBlocks
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => kvp.Value.Build())
            .Where(b => b is not null)
            .Select(b => b!)
            .ToList();

        yield return new AnthropicStreamItem.MessageBuilt(finalBlocks);
    }

    private static IEnumerable<AnthropicStreamItem> ProcessEvent(
        string eventName,
        string data,
        Dictionary<int, ContentBlockBuilder> blocks)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;

        switch (eventName)
        {
            case "content_block_start":
                {
                    var index = root.GetProperty("index").GetInt32();
                    var blockElement = root.GetProperty("content_block");
                    var type = blockElement.GetProperty("type").GetString();
                    var builder = new ContentBlockBuilder { Type = type ?? "" };
                    if (type == "tool_use")
                    {
                        builder.ToolUseId = blockElement.GetProperty("id").GetString();
                        builder.ToolName = blockElement.GetProperty("name").GetString();
                        yield return new AnthropicStreamItem.ToolUseStarted(
                            builder.ToolUseId ?? "",
                            builder.ToolName ?? "");
                    }
                    blocks[index] = builder;
                    break;
                }
            case "content_block_delta":
                {
                    var index = root.GetProperty("index").GetInt32();
                    if (!blocks.TryGetValue(index, out var builder))
                    {
                        yield break;
                    }
                    var delta = root.GetProperty("delta");
                    var deltaType = delta.GetProperty("type").GetString();
                    if (deltaType == "text_delta")
                    {
                        var text = delta.GetProperty("text").GetString() ?? "";
                        builder.TextBuffer.Append(text);
                        yield return new AnthropicStreamItem.TextDelta(text);
                    }
                    else if (deltaType == "input_json_delta")
                    {
                        var partial = delta.GetProperty("partial_json").GetString() ?? "";
                        builder.JsonBuffer.Append(partial);
                    }
                    break;
                }
            case "content_block_stop":
                {
                    break;
                }
            case "message_delta":
                {
                    if (root.TryGetProperty("delta", out var delta)
                        && delta.TryGetProperty("stop_reason", out var stopReason)
                        && stopReason.ValueKind == JsonValueKind.String)
                    {
                        yield return new AnthropicStreamItem.StopReason(stopReason.GetString() ?? "");
                    }
                    break;
                }
            case "error":
                {
                    var message = root.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg)
                        ? msg.GetString() ?? "Unknown API error"
                        : "Unknown API error";
                    throw new InvalidOperationException($"Anthropic stream error: {message}");
                }
        }
    }

    private sealed class ContentBlockBuilder
    {
        public string Type { get; set; } = "";
        public StringBuilder TextBuffer { get; } = new();
        public StringBuilder JsonBuffer { get; } = new();
        public string? ToolUseId { get; set; }
        public string? ToolName { get; set; }

        public AnthropicContentBlock? Build()
        {
            if (Type == "text")
            {
                return new AnthropicTextBlock { Text = TextBuffer.ToString() };
            }
            if (Type == "tool_use")
            {
                var json = JsonBuffer.Length == 0 ? "{}" : JsonBuffer.ToString();
                JsonElement input;
                try
                {
                    input = JsonSerializer.Deserialize<JsonElement>(json);
                }
                catch (JsonException)
                {
                    input = JsonSerializer.Deserialize<JsonElement>("{}");
                }
                return new AnthropicToolUseBlock
                {
                    Id = ToolUseId ?? "",
                    Name = ToolName ?? "",
                    Input = input,
                };
            }
            return null;
        }
    }
}

internal abstract record AnthropicStreamItem
{
    public sealed record TextDelta(string Text) : AnthropicStreamItem;
    public sealed record ToolUseStarted(string Id, string Name) : AnthropicStreamItem;
    public sealed record StopReason(string Reason) : AnthropicStreamItem;
    public sealed record MessageBuilt(List<AnthropicContentBlock> Blocks) : AnthropicStreamItem;
}
