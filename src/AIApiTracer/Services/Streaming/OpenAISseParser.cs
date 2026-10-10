using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIApiTracer.Services.Streaming;

/// <summary>
/// SSE parser for OpenAI API responses
/// </summary>
public class OpenAISseParser : ISseParser
{
    public bool CanParse(string targetUrl)
    {
        // Only handle completions endpoints (not embeddings, images, etc.)
        if (!targetUrl.Contains("/completions", StringComparison.OrdinalIgnoreCase))
            return false;
            
        return targetUrl.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase) ||
               targetUrl.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase) ||
               targetUrl.Contains("api.x.ai", StringComparison.OrdinalIgnoreCase) ||
               (targetUrl.Contains("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase) && 
                targetUrl.Contains("/openai/", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<string> ParseSseStreamAsync(Stream stream)
    {
        var chunks = new List<JsonElement>();
        var choiceBuilders = new Dictionary<int, ChoiceBuilder>();
        JsonElement? firstChunk = null;
        string? id = null;
        string? model = null;
        string? systemFingerprint = null;
        JsonElement? usage = null;
        var extraProperties = new Dictionary<string, JsonElement>();

        // Ensure stream position is at the beginning
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var parser = SseParser.Create(stream);
        
        try
        {
            await foreach (var sseItem in parser.EnumerateAsync())
            {
                var data = sseItem.Data;
                
                if (string.IsNullOrEmpty(data))
                    continue;

                if (data == "[DONE]")
                    break;

                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var root = doc.RootElement;
                    chunks.Add(root.Clone());

                    // Capture first chunk
                    if (firstChunk == null)
                    {
                        firstChunk = root.Clone();
                    }
                    
                    // Extract extra properties from all chunks (merge them)
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Name != "id" && prop.Name != "object" && prop.Name != "created" && 
                            prop.Name != "model" && prop.Name != "system_fingerprint" && 
                            prop.Name != "choices" && prop.Name != "usage")
                        {
                            // Always update/add extra properties to capture properties from later chunks
                            extraProperties[prop.Name] = prop.Value.Clone();
                        }
                    }

                    // Capture common fields (skip empty values)
                    if (id == null && root.TryGetProperty("id", out var idProp))
                    {
                        var idValue = idProp.GetString();
                        if (!string.IsNullOrEmpty(idValue))
                            id = idValue;
                    }
                    if (model == null && root.TryGetProperty("model", out var modelProp))
                    {
                        var modelValue = modelProp.GetString();
                        if (!string.IsNullOrEmpty(modelValue))
                            model = modelValue;
                    }
                    if (systemFingerprint == null && root.TryGetProperty("system_fingerprint", out var sysFpProp))
                    {
                        var sysFpValue = sysFpProp.GetString();
                        if (!string.IsNullOrEmpty(sysFpValue))
                            systemFingerprint = sysFpValue;
                    }

                    // Process choices
                    if (root.TryGetProperty("choices", out var choices))
                    {
                        foreach (var choice in choices.EnumerateArray())
                        {
                            if (!choice.TryGetProperty("index", out var indexProp))
                                continue;

                            var index = indexProp.GetInt32();
                            
                            if (!choiceBuilders.ContainsKey(index))
                            {
                                choiceBuilders[index] = new ChoiceBuilder { Index = index };
                                
                                // Capture extra properties from first choice occurrence
                                foreach (var prop in choice.EnumerateObject())
                                {
                                    if (prop.Name != "index" && prop.Name != "delta" && 
                                        prop.Name != "finish_reason" && prop.Name != "message")
                                    {
                                        choiceBuilders[index].ExtraProperties[prop.Name] = prop.Value.Clone();
                                    }
                                }
                            }

                            var builder = choiceBuilders[index];

                            // Process delta
                            if (choice.TryGetProperty("delta", out var delta))
                            {
                                if (delta.TryGetProperty("role", out var roleProp))
                                    builder.Role = roleProp.GetString() ?? "assistant";
                                
                                if (delta.TryGetProperty("content", out var contentProp) && 
                                    contentProp.ValueKind == JsonValueKind.String)
                                {
                                    builder.ContentBuilder.Append(contentProp.GetString());
                                }

                                // Process tool_calls
                                if (delta.TryGetProperty("tool_calls", out var toolCalls))
                                {
                                    foreach (var toolCall in toolCalls.EnumerateArray())
                                    {
                                        if (!toolCall.TryGetProperty("index", out var toolIndexProp))
                                            continue;

                                        var toolIndex = toolIndexProp.GetInt32();
                                        
                                        if (!builder.ToolCallBuilders.ContainsKey(toolIndex))
                                        {
                                            builder.ToolCallBuilders[toolIndex] = new ToolCallBuilder { Index = toolIndex };
                                        }

                                        var toolCallBuilder = builder.ToolCallBuilders[toolIndex];

                                        // Extract tool call properties
                                        if (toolCall.TryGetProperty("id", out var toolIdProp))
                                            toolCallBuilder.Id = toolIdProp.GetString();
                                        
                                        if (toolCall.TryGetProperty("type", out var toolTypeProp))
                                            toolCallBuilder.Type = toolTypeProp.GetString();
                                        
                                        if (toolCall.TryGetProperty("function", out var function))
                                        {
                                            if (function.TryGetProperty("name", out var nameProp))
                                                toolCallBuilder.FunctionName = nameProp.GetString();
                                            
                                            if (function.TryGetProperty("arguments", out var argsProp) && 
                                                argsProp.ValueKind == JsonValueKind.String)
                                            {
                                                toolCallBuilder.ArgumentsBuilder.Append(argsProp.GetString());
                                            }
                                        }
                                    }
                                }

                                // Capture extra delta properties
                                foreach (var prop in delta.EnumerateObject())
                                {
                                    if (prop.Name != "role" && prop.Name != "content" && prop.Name != "tool_calls")
                                    {
                                        builder.DeltaExtraProperties[prop.Name] = prop.Value.Clone();
                                    }
                                }
                            }

                            // Capture finish reason
                            if (choice.TryGetProperty("finish_reason", out var finishReasonProp) && 
                                finishReasonProp.ValueKind == JsonValueKind.String)
                            {
                                builder.FinishReason = finishReasonProp.GetString();
                            }
                        }
                    }

                    // Capture usage from final chunk
                    if (root.TryGetProperty("usage", out var usageProp))
                    {
                        usage = usageProp.Clone();
                    }
                }
                catch (JsonException)
                {
                    // Skip invalid JSON chunks
                    continue;
                }
            }
        }
        catch (Exception)
        {
            // If parsing fails completely, try the fallback method
            if (stream.CanSeek)
            {
                stream.Position = 0;
                using var reader = new StreamReader(stream);
                var data = await reader.ReadToEndAsync();
                return ParseSseResponse(data) ?? "{}";
            }
            throw;
        }

        // If we have no valid chunks, return empty JSON object
        if (chunks.Count == 0)
            return "{}";

        // Build the complete response
        using var output = new MemoryStream();
        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
        
        writer.WriteStartObject();
        
        // Write standard properties
        writer.WriteString("id", id ?? "");
        writer.WriteString("object", firstChunk?.TryGetProperty("object", out var objProp) == true ? 
            objProp.GetString() : "chat.completion");
        writer.WriteNumber("created", firstChunk?.TryGetProperty("created", out var createdProp) == true ? 
            createdProp.GetInt64() : 0);
        writer.WriteString("model", model ?? "");
        
        if (systemFingerprint != null)
            writer.WriteString("system_fingerprint", systemFingerprint);

        // Write choices
        writer.WriteStartArray("choices");
        foreach (var kvp in choiceBuilders.OrderBy(x => x.Key))
        {
            writer.WriteStartObject();
            writer.WriteNumber("index", kvp.Value.Index);
            
            writer.WriteStartObject("message");
            writer.WriteString("role", kvp.Value.Role);
            
            var content = kvp.Value.ContentBuilder.ToString();
            if (!string.IsNullOrEmpty(content))
            {
                writer.WriteString("content", content);
            }
            else
            {
                writer.WriteNull("content");
            }
            
            // Write tool_calls if any
            if (kvp.Value.ToolCallBuilders.Count > 0)
            {
                writer.WriteStartArray("tool_calls");
                foreach (var toolKvp in kvp.Value.ToolCallBuilders.OrderBy(x => x.Key))
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", toolKvp.Value.Id ?? "");
                    writer.WriteString("type", toolKvp.Value.Type ?? "function");
                    
                    writer.WriteStartObject("function");
                    writer.WriteString("name", toolKvp.Value.FunctionName ?? "");
                    writer.WriteString("arguments", toolKvp.Value.ArgumentsBuilder.ToString());
                    writer.WriteEndObject(); // function
                    
                    writer.WriteEndObject(); // tool_call
                }
                writer.WriteEndArray(); // tool_calls
            }
            
            // Write extra delta properties to message
            foreach (var prop in kvp.Value.DeltaExtraProperties)
            {
                writer.WritePropertyName(prop.Key);
                prop.Value.WriteTo(writer);
            }
            
            writer.WriteEndObject(); // message
            
            if (kvp.Value.FinishReason != null)
                writer.WriteString("finish_reason", kvp.Value.FinishReason);
            
            // Write extra choice properties
            foreach (var prop in kvp.Value.ExtraProperties)
            {
                writer.WritePropertyName(prop.Key);
                prop.Value.WriteTo(writer);
            }
            
            writer.WriteEndObject(); // choice
        }
        writer.WriteEndArray(); // choices

        // Write usage if available
        if (usage != null)
        {
            writer.WritePropertyName("usage");
            usage.Value.WriteTo(writer);
        }

        // Write extra properties from the response
        foreach (var prop in extraProperties)
        {
            writer.WritePropertyName(prop.Key);
            prop.Value.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.Flush();
        
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private class ChoiceBuilder
    {
        public int Index { get; set; }
        public string Role { get; set; } = "assistant";
        public StringBuilder ContentBuilder { get; } = new();
        public string? FinishReason { get; set; }
        public Dictionary<string, JsonElement> ExtraProperties { get; } = new();
        public Dictionary<string, JsonElement> DeltaExtraProperties { get; } = new();
        public Dictionary<int, ToolCallBuilder> ToolCallBuilders { get; } = new();
    }

    private class ToolCallBuilder
    {
        public int Index { get; set; }
        public string? Id { get; set; }
        public string? Type { get; set; }
        public string? FunctionName { get; set; }
        public StringBuilder ArgumentsBuilder { get; } = new();
    }

    // Keep the ParseSseResponse method for testing
    internal string? ParseSseResponse(string sseData)
    {
        if (string.IsNullOrWhiteSpace(sseData))
            return null;

        var lines = sseData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<OpenAIStreamChunk>();
        var contentBuilder = new StringBuilder();
        string? id = null;
        string? model = null;
        string? systemFingerprint = null;
        OpenAIUsage? usage = null;
        string? finishReason = null;

        foreach (var line in lines)
        {
            if (!line.StartsWith("data: "))
                continue;

            var data = line.Substring(6).Trim();
            if (data == "[DONE]")
                break;

            try
            {
                var chunk = JsonSerializer.Deserialize<OpenAIStreamChunk>(data);
                if (chunk != null)
                {
                    chunks.Add(chunk);

                    // Capture common fields from first chunk
                    if (id == null && !string.IsNullOrEmpty(chunk.Id))
                        id = chunk.Id;
                    if (model == null && !string.IsNullOrEmpty(chunk.Model))
                        model = chunk.Model;
                    if (systemFingerprint == null && !string.IsNullOrEmpty(chunk.SystemFingerprint))
                        systemFingerprint = chunk.SystemFingerprint;

                    // Accumulate content
                    if (chunk.Choices?.Count > 0)
                    {
                        var delta = chunk.Choices[0].Delta;
                        if (delta?.Content != null)
                        {
                            contentBuilder.Append(delta.Content);
                        }
                    }

                    // Capture finish reason
                    if (chunk.Choices?.Count > 0 && !string.IsNullOrEmpty(chunk.Choices[0].FinishReason))
                    {
                        finishReason = chunk.Choices[0].FinishReason;
                    }

                    // Capture usage from final chunk
                    if (chunk.Usage != null)
                    {
                        usage = chunk.Usage;
                    }
                }
            }
            catch (JsonException)
            {
                // Skip invalid JSON chunks
                continue;
            }
        }

        // If we have no valid chunks, return null
        if (chunks.Count == 0)
            return null;

        // Build the complete response
        var response = new OpenAIResponse
        {
            Id = id ?? "",
            Object = "chat.completion",
            Created = chunks.FirstOrDefault()?.Created ?? 0,
            Model = model ?? "",
            SystemFingerprint = systemFingerprint,
            Choices = new List<Choice>
            {
                new Choice
                {
                    Index = 0,
                    Message = new Message
                    {
                        Role = "assistant",
                        Content = contentBuilder.ToString()
                    },
                    FinishReason = finishReason
                }
            },
            Usage = usage
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }

    private class OpenAIStreamChunk
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        
        [JsonPropertyName("object")]
        public string? Object { get; set; }
        
        [JsonPropertyName("created")]
        public long Created { get; set; }
        
        [JsonPropertyName("model")]
        public string? Model { get; set; }
        
        [JsonPropertyName("system_fingerprint")]
        public string? SystemFingerprint { get; set; }
        
        [JsonPropertyName("choices")]
        public List<StreamChoice>? Choices { get; set; }
        
        [JsonPropertyName("usage")]
        public OpenAIUsage? Usage { get; set; }
    }

    private class StreamChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }
        
        [JsonPropertyName("delta")]
        public Delta? Delta { get; set; }
        
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private class Delta
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }
        
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class OpenAIResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        
        [JsonPropertyName("object")]
        public string Object { get; set; } = "";
        
        [JsonPropertyName("created")]
        public long Created { get; set; }
        
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";
        
        [JsonPropertyName("system_fingerprint")]
        public string? SystemFingerprint { get; set; }
        
        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; } = new();
        
        [JsonPropertyName("usage")]
        public OpenAIUsage? Usage { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }
        
        [JsonPropertyName("message")]
        public Message Message { get; set; } = new();
        
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private class Message
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";
        
        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private class OpenAIUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; }
        
        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; set; }
        
        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; set; }
        
        [JsonPropertyName("prompt_tokens_details")]
        public PromptTokensDetails? PromptTokensDetails { get; set; }
        
        [JsonPropertyName("completion_tokens_details")]
        public CompletionTokensDetails? CompletionTokensDetails { get; set; }
    }

    private class PromptTokensDetails
    {
        [JsonPropertyName("cached_tokens")]
        public int? CachedTokens { get; set; }
        
        [JsonPropertyName("audio_tokens")]
        public int? AudioTokens { get; set; }
    }

    private class CompletionTokensDetails
    {
        [JsonPropertyName("reasoning_tokens")]
        public int? ReasoningTokens { get; set; }
        
        [JsonPropertyName("audio_tokens")]
        public int? AudioTokens { get; set; }
        
        [JsonPropertyName("accepted_prediction_tokens")]
        public int? AcceptedPredictionTokens { get; set; }
        
        [JsonPropertyName("rejected_prediction_tokens")]
        public int? RejectedPredictionTokens { get; set; }
    }
}