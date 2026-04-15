namespace project_approval_system.Services.Chatbot;

public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string Model { get; set; } = "claude-haiku-4-5-20251001";

    public int MaxTokens { get; set; } = 2048;

    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    public string SystemPromptPath { get; set; } = "Services/Chatbot/system-prompt.md";

    public int MaxToolIterations { get; set; } = 8;
}
