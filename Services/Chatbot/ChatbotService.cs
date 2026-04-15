using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Options;
using project_approval_system.Data;
using project_approval_system.Services.Chatbot.Models;

namespace project_approval_system.Services.Chatbot;

internal sealed class ChatbotService(
    AnthropicClient client,
    IChatToolRegistry toolRegistry,
    IOptions<AnthropicOptions> options,
    IWebHostEnvironment env,
    ILogger<ChatbotService> logger) : IChatbotService
{
    private readonly AnthropicOptions _options = options.Value;
    private string? _cachedSystemPromptTemplate;

    public async IAsyncEnumerable<ChatTurnEvent> RunTurnAsync(
        IList<ChatMessage> history,
        string userInput,
        ClaimsPrincipal user,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var ctx = BuildContext(user);
        if (ctx is null)
        {
            yield return new TurnErrorEvent("You must be signed in with a recognised role to use the assistant.");
            yield break;
        }

        history.Add(ChatMessage.UserText(userInput));

        var toolDefs = toolRegistry.ForUser(user);
        var systemPrompt = BuildSystemPrompt(ctx.Value);

        var anthropicTools = toolDefs.Select(t => new AnthropicTool
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = t.InputSchema,
        }).ToList();

        for (var iteration = 0; iteration < _options.MaxToolIterations; iteration++)
        {
            var request = new AnthropicMessagesRequest
            {
                Model = _options.Model,
                MaxTokens = _options.MaxTokens,
                System = systemPrompt,
                Messages = history.Select(ToAnthropicMessage).ToList(),
                Tools = anthropicTools.Count > 0 ? anthropicTools : null,
            };

            List<AnthropicContentBlock>? assistantBlocks = null;
            string? stopReason = null;
            Exception? streamError = null;

            var stream = client.StreamAsync(request, ct);
            var enumerator = stream.GetAsyncEnumerator(ct);
            try
            {
                while (true)
                {
                    AnthropicStreamItem? item = null;
                    try
                    {
                        if (!await enumerator.MoveNextAsync())
                        {
                            break;
                        }
                        item = enumerator.Current;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        streamError = ex;
                        break;
                    }

                    switch (item)
                    {
                        case AnthropicStreamItem.TextDelta td:
                            yield return new TextDeltaEvent(td.Text);
                            break;
                        case AnthropicStreamItem.ToolUseStarted tus:
                            yield return new ToolCallStartedEvent(tus.Id, tus.Name);
                            break;
                        case AnthropicStreamItem.StopReason sr:
                            stopReason = sr.Reason;
                            break;
                        case AnthropicStreamItem.MessageBuilt mb:
                            assistantBlocks = mb.Blocks;
                            break;
                    }
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            if (streamError is not null)
            {
                logger.LogError(streamError, "Chatbot stream failed");
                yield return new TurnErrorEvent("The assistant couldn't reach the model. Please try again.");
                yield break;
            }

            if (assistantBlocks is null)
            {
                yield return new TurnErrorEvent("The assistant returned no content.");
                yield break;
            }

            var assistantMessage = ToChatMessage(assistantBlocks);
            history.Add(assistantMessage);
            yield return new AssistantMessageCompletedEvent(assistantMessage);

            if (stopReason != "tool_use")
            {
                yield return new TurnCompletedEvent(stopReason ?? "end_turn");
                yield break;
            }

            var toolUseBlocks = assistantBlocks.OfType<AnthropicToolUseBlock>().ToList();
            if (toolUseBlocks.Count == 0)
            {
                yield return new TurnCompletedEvent(stopReason);
                yield break;
            }

            var toolResults = new List<ChatContentBlock>();
            foreach (var call in toolUseBlocks)
            {
                string resultJson;
                bool isError;
                try
                {
                    resultJson = await toolRegistry.ExecuteAsync(call.Name, call.Input, ctx.Value, ct);
                    isError = false;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Tool '{Tool}' failed", call.Name);
                    resultJson = JsonSerializer.Serialize(new { error = ex.Message });
                    isError = true;
                }

                toolResults.Add(new ChatContentBlock
                {
                    Type = ChatContentType.ToolResult,
                    ToolUseId = call.Id,
                    ToolResultContent = resultJson,
                    ToolResultIsError = isError,
                });

                yield return new ToolCallFinishedEvent(call.Id, call.Name, resultJson.Length, isError);
            }

            history.Add(new ChatMessage { Role = ChatRole.User, Content = toolResults });
        }

        yield return new TurnErrorEvent($"Tool-use loop exceeded the {_options.MaxToolIterations}-iteration limit.");
    }

    private static ChatToolContext? BuildContext(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }
        var name = user.Identity?.Name ?? "";
        var role = Roles.All.FirstOrDefault(user.IsInRole);
        return role is null ? null : new ChatToolContext(id, name, role);
    }

    private string BuildSystemPrompt(ChatToolContext ctx)
    {
        _cachedSystemPromptTemplate ??= LoadSystemPromptTemplate();
        return _cachedSystemPromptTemplate
            .Replace("{{DisplayName}}", string.IsNullOrWhiteSpace(ctx.DisplayName) ? "the signed-in user" : ctx.DisplayName)
            .Replace("{{Role}}", ctx.Role)
            .Replace("{{UtcDate}}", DateTime.UtcNow.ToString("yyyy-MM-dd"));
    }

    private string LoadSystemPromptTemplate()
    {
        var path = Path.IsPathRooted(_options.SystemPromptPath)
            ? _options.SystemPromptPath
            : Path.Combine(env.ContentRootPath, _options.SystemPromptPath);

        if (!File.Exists(path))
        {
            logger.LogWarning("System prompt file not found at {Path}; using fallback.", path);
            return "You are the Blind-Match assistant. The signed-in user is {{DisplayName}}, role {{Role}}. Today is {{UtcDate}}. Use only the provided tools.";
        }
        return File.ReadAllText(path);
    }

    private static AnthropicMessage ToAnthropicMessage(ChatMessage message)
    {
        return new AnthropicMessage
        {
            Role = message.Role == ChatRole.User ? "user" : "assistant",
            Content = message.Content.Select<ChatContentBlock, AnthropicContentBlock>(b => b.Type switch
            {
                ChatContentType.Text => new AnthropicTextBlock { Text = b.Text ?? "" },
                ChatContentType.ToolUse => new AnthropicToolUseBlock
                {
                    Id = b.ToolUseId ?? "",
                    Name = b.ToolName ?? "",
                    Input = b.ToolInput ?? JsonDocument.Parse("{}").RootElement,
                },
                ChatContentType.ToolResult => new AnthropicToolResultBlock
                {
                    ToolUseId = b.ToolUseId ?? "",
                    Content = b.ToolResultContent ?? "",
                    IsError = b.ToolResultIsError,
                },
                _ => throw new InvalidOperationException($"Unknown content type {b.Type}"),
            }).ToList(),
        };
    }

    private static ChatMessage ToChatMessage(List<AnthropicContentBlock> blocks)
    {
        var converted = new List<ChatContentBlock>();
        foreach (var block in blocks)
        {
            switch (block)
            {
                case AnthropicTextBlock t:
                    converted.Add(new ChatContentBlock { Type = ChatContentType.Text, Text = t.Text });
                    break;
                case AnthropicToolUseBlock tu:
                    converted.Add(new ChatContentBlock
                    {
                        Type = ChatContentType.ToolUse,
                        ToolUseId = tu.Id,
                        ToolName = tu.Name,
                        ToolInput = tu.Input,
                    });
                    break;
            }
        }
        return new ChatMessage { Role = ChatRole.Assistant, Content = converted };
    }
}
