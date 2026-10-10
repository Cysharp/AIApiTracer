using System.Text.Json;
using AIApiTracer.Models;

namespace AIApiTracer.Services.MessageParsing;

/// <summary>
/// Parser for Gemini API message format
/// </summary>
public class GeminiMessageParser : BaseMessageParser
{
    private static readonly HashSet<string> KnownRequestFields = new()
    {
        "contents", "systemInstruction"
    };

    private static readonly HashSet<string> KnownResponseFields = new()
    {
        "candidates"
    };

    public override bool CanParse(EndpointType endpointType)
    {
        return endpointType == EndpointType.Gemini;
    }

    public override ParsedMessageData Parse(string json, bool isRequest)
    {
        var result = new ParsedMessageData();

        using var document = TryParseJson(json);
        if (document == null)
            return result;

        var root = document.RootElement;

        if (isRequest)
        {
            ParseRequest(root, result);
        }
        else
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                // Native Gemini streaming returns an array of response objects
                foreach (var chunk in root.EnumerateArray())
                {
                    ParseResponse(chunk, result);
                }
                
                // Merge multiple assistant messages from chunks into a single message if possible
                MergeAssistantMessages(result);
            }
            else
            {
                ParseResponse(root, result);
            }
        }

        return result;
    }

    private void MergeAssistantMessages(ParsedMessageData result)
    {
        if (result.Messages.Count <= 1)
            return;

        var mergedMessages = new List<ParsedMessage>();
        ParsedMessage? lastAssistantMessage = null;

        foreach (var message in result.Messages)
        {
            if (message.Role == "assistant")
            {
                if (lastAssistantMessage == null)
                {
                    lastAssistantMessage = message;
                    mergedMessages.Add(lastAssistantMessage);
                }
                else
                {
                    // Merge content
                    if (!string.IsNullOrEmpty(message.Content))
                    {
                        lastAssistantMessage.Content = (lastAssistantMessage.Content ?? "") + message.Content;
                    }
                    
                    // Merge content parts
                    if (message.ContentParts != null)
                    {
                        lastAssistantMessage.ContentParts ??= new List<ContentPart>();
                        lastAssistantMessage.ContentParts.AddRange(message.ContentParts);
                    }
                    
                    // Merge tool calls
                    if (message.ToolCalls != null)
                    {
                        lastAssistantMessage.ToolCalls ??= new List<ParsedToolCall>();
                        lastAssistantMessage.ToolCalls.AddRange(message.ToolCalls);
                    }
                    
                    // Merge other data
                    if (message.OtherData != null)
                    {
                        lastAssistantMessage.OtherData ??= new Dictionary<string, JsonElement>();
                        foreach (var kvp in message.OtherData)
                        {
                            lastAssistantMessage.OtherData[kvp.Key] = kvp.Value.Clone();
                        }
                    }
                }
            }
            else
            {
                mergedMessages.Add(message);
                lastAssistantMessage = null;
            }
        }

        result.Messages = mergedMessages;
    }

    private void ParseRequest(JsonElement root, ParsedMessageData result)
    {
        // Parse system instruction if present
        if (root.TryGetProperty("systemInstruction", out var systemInstructionElement))
        {
            var systemMessage = ParseGeminiContent(systemInstructionElement, "system");
            if (systemMessage != null)
            {
                result.Messages.Add(systemMessage);
            }
        }

        // Parse messages (contents)
        if (root.TryGetProperty("contents", out var contentsElement))
        {
            if (contentsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var contentElement in contentsElement.EnumerateArray())
                {
                    var message = ParseGeminiContent(contentElement);
                    if (message != null)
                    {
                        result.Messages.Add(message);
                    }
                }
            }
            else
            {
                var message = ParseGeminiContent(contentsElement);
                if (message != null)
                {
                    result.Messages.Add(message);
                }
            }
        }

        // Extract other data
        result.OtherData = ExtractOtherData(root, KnownRequestFields);
    }

    private void ParseResponse(JsonElement root, ParsedMessageData result)
    {
        // Parse candidates
        if (root.TryGetProperty("candidates", out var candidatesElement))
        {
            foreach (var candidateElement in candidatesElement.EnumerateArray())
            {
                if (candidateElement.TryGetProperty("content", out var contentElement))
                {
                    var message = ParseGeminiContent(contentElement);
                    if (message != null)
                    {
                        // Add finish reason as other data to the message
                        if (candidateElement.TryGetProperty("finishReason", out var finishReason))
                        {
                            message.OtherData ??= new Dictionary<string, JsonElement>();
                            message.OtherData["finishReason"] = finishReason.Clone();
                        }
                        
                        result.Messages.Add(message);
                    }
                }
            }
        }

        // Extract other data
        result.OtherData = ExtractOtherData(root, KnownResponseFields);
    }

    private ParsedMessage? ParseGeminiContent(JsonElement contentElement, string? defaultRole = null)
    {
        var message = new ParsedMessage();
        
        // Get role
        if (contentElement.TryGetProperty("role", out var roleProp))
        {
            message.Role = roleProp.GetString() ?? defaultRole ?? "unknown";
        }
        else
        {
            message.Role = defaultRole ?? "user";
        }

        // Gemini uses 'model' instead of 'assistant'
        if (message.Role == "model")
        {
            message.Role = "assistant";
        }

        // Parse parts
        if (contentElement.TryGetProperty("parts", out var partsElement))
        {
            message.ContentParts = new List<ContentPart>();
            message.ToolCalls = new List<ParsedToolCall>();

            foreach (var partElement in partsElement.EnumerateArray())
            {
                // Text part
                if (partElement.TryGetProperty("text", out var textProp))
                {
                    message.ContentParts.Add(new ContentPart
                    {
                        Type = "text",
                        Text = textProp.GetString()
                    });
                }
                // Inline data (image)
                else if (partElement.TryGetProperty("inlineData", out var inlineDataProp))
                {
                    message.ContentParts.Add(new ContentPart
                    {
                        Type = "image",
                        OtherData = new Dictionary<string, JsonElement> { { "inlineData", inlineDataProp.Clone() } }
                    });
                }
                // Function call
                else if (partElement.TryGetProperty("functionCall", out var functionCallProp))
                {
                    var toolCall = new ParsedToolCall
                    {
                        Id = Guid.NewGuid().ToString(), // Gemini native doesn't always have IDs for tool calls
                        Name = functionCallProp.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "",
                        Arguments = functionCallProp.TryGetProperty("args", out var argsProp) ? argsProp.Clone() : null
                    };
                    message.ToolCalls.Add(toolCall);
                }
                // Function response
                else if (partElement.TryGetProperty("functionResponse", out var functionResponseProp))
                {
                    message.Role = "tool";
                    var toolCall = new ParsedToolCall
                    {
                        Name = functionResponseProp.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "",
                        Result = functionResponseProp.TryGetProperty("response", out var respProp) ? respProp.GetRawText() : null
                    };
                    message.ToolCalls.Add(toolCall);
                }
            }

            // Set simple content if there is only one text part
            if (message.ContentParts.Count == 1 && message.ContentParts[0].Type == "text")
            {
                message.Content = message.ContentParts[0].Text;
                message.ContentParts = null;
            }
            else if (message.ContentParts.Count == 0)
            {
                message.ContentParts = null;
            }
            
            if (message.ToolCalls.Count == 0)
            {
                message.ToolCalls = null;
            }
        }

        return message;
    }
}
