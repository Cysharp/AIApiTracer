using System.Text.Json;
using System.Text.Json.Serialization;
using AIApiTracer.Models;

namespace AIApiTracer.Services.Metadata;

/// <summary>
/// Extracts AI metadata from xAI API responses
/// </summary>
public class XAIMetadataExtractor : BaseAiMetadataExtractor
{
    public override bool CanExtract(string targetUrl)
    {
        return targetUrl.Contains("api.x.ai", StringComparison.OrdinalIgnoreCase);
    }

    public override AiMetadata? ExtractMetadata(string? targetUrl, string? requestBody, string? responseBody, Dictionary<string, string[]> responseHeaders)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        // xAI uses the same response format as OpenAI
        var response = TryDeserialize<XAIResponse>(responseBody);
        if (response == null)
            return null;

        var metadata = new AiMetadata
        {
            Model = response.Model
        };

        if (response.Usage != null)
        {
            metadata.Usage = new TokenUsage
            {
                InputTokens = response.Usage.PromptTokens,
                OutputTokens = response.Usage.CompletionTokens,
                TotalTokens = response.Usage.TotalTokens
            };
        }

        // Add system fingerprint to extra metadata if present
        if (!string.IsNullOrEmpty(response.SystemFingerprint))
        {
            metadata.Extra["system_fingerprint"] = response.SystemFingerprint;
        }

        // Add finish reason from first choice if available
        if (response.Choices?.Count > 0)
        {
            var finishReason = response.Choices[0].FinishReason;
            if (!string.IsNullOrEmpty(finishReason))
            {
                metadata.Extra["finish_reason"] = finishReason;
            }
        }

        return metadata;
    }

    private class XAIResponse
    {
        public string? Id { get; set; }
        public string? Object { get; set; }
        public long? Created { get; set; }
        public string? Model { get; set; }
        
        [JsonPropertyName("system_fingerprint")]
        public string? SystemFingerprint { get; set; }
        
        public List<Choice>? Choices { get; set; }
        public XAIUsage? Usage { get; set; }
    }

    private class Choice
    {
        public int? Index { get; set; }
        
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private class XAIUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; }
        
        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; set; }
        
        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; set; }
    }
}