using System.Text.Json;

namespace AIApiTracer.Services.MessageParsing;

/// <summary>
/// Interface for parsing AI API messages
/// </summary>
public interface IMessageParser
{
    /// <summary>
    /// Parses a JSON request or response body into structured messages
    /// </summary>
    /// <param name="json">The JSON string to parse</param>
    /// <param name="isRequest">Whether this is a request (true) or response (false)</param>
    /// <returns>Parsed message data</returns>
    ParsedMessageData Parse(string json, bool isRequest);

    /// <summary>
    /// Determines if this parser can handle the given endpoint type
    /// </summary>
    bool CanParse(EndpointType endpointType);
}

/// <summary>
/// Represents the parsed message data
/// </summary>
public class ParsedMessageData
{
    public List<ParsedMessage> Messages { get; set; } = new();
    public List<ParsedToolCall> ToolCalls { get; set; } = new();
    public Dictionary<string, JsonElement> OtherData { get; set; } = new();
}

/// <summary>
/// Represents a parsed message with role and content
/// </summary>
public class ParsedMessage
{
    public string Role { get; set; } = string.Empty;
    public string? Content { get; set; }
    public List<ContentPart>? ContentParts { get; set; }
    public List<ParsedToolCall>? ToolCalls { get; set; }
    public Dictionary<string, JsonElement>? OtherData { get; set; }
}

/// <summary>
/// Represents a content part (for multimodal messages)
/// </summary>
public class ContentPart
{
    public string Type { get; set; } = string.Empty;
    public string? Text { get; set; }
    public string? ImageUrl { get; set; }
    public Dictionary<string, JsonElement>? OtherData { get; set; }
}

/// <summary>
/// Represents a tool call
/// </summary>
public class ParsedToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "function";
    public string Name { get; set; } = string.Empty;
    public JsonElement? Arguments { get; set; }
    public string? Result { get; set; }
}

/// <summary>
/// Endpoint types for AI services
/// </summary>
public enum EndpointType
{
    Unknown,
    OpenAI,
    Anthropic,
    AzureOpenAI,
    xAI,
    OpenAICompat,
    Gemini
}