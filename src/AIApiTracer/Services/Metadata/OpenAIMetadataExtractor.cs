using System.Text.Json;
using System.Text.Json.Serialization;
using AIApiTracer.Models;

namespace AIApiTracer.Services.Metadata;

/// <summary>
/// Extracts AI metadata from OpenAI API responses
/// </summary>
public class OpenAIMetadataExtractor : BaseAiMetadataExtractor
{
    public override bool CanExtract(string targetUrl)
    {
        return targetUrl.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase) ||
               targetUrl.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase);
    }

    public override AiMetadata? ExtractMetadata(string? targetUrl, string? requestBody, string? responseBody, Dictionary<string, string[]> responseHeaders)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        var response = TryDeserialize<OpenAIResponse>(responseBody);
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
                TotalTokens = response.Usage.TotalTokens,
                CachedTokens = response.Usage.PromptTokensDetails?.CachedTokens,
                InputTokensCached = response.Usage.PromptTokensDetails?.CachedTokens
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

        // Extract rate limit information from headers
        metadata.RateLimit = ExtractRateLimitInfo(responseHeaders);

        return metadata;
    }

    private RateLimitInfo? ExtractRateLimitInfo(Dictionary<string, string[]> responseHeaders)
    {
        if (responseHeaders == null || responseHeaders.Count == 0)
            return null;

        var rateLimitInfo = new RateLimitInfo();
        var hasRateLimitData = false;

        // OpenAI and Azure OpenAI use x-ratelimit-* headers
        if (TryGetHeaderValue(responseHeaders, "x-ratelimit-remaining-requests", out var remainingRequests) && int.TryParse(remainingRequests, out var remainingRequestsInt))
        {
            rateLimitInfo.RemainingRequests = remainingRequestsInt;
            hasRateLimitData = true;
        }

        if (TryGetHeaderValue(responseHeaders, "x-ratelimit-limit-requests", out var limitRequests) && int.TryParse(limitRequests, out var limitRequestsInt))
        {
            rateLimitInfo.LimitRequests = limitRequestsInt;
            hasRateLimitData = true;
        }

        if (TryGetHeaderValue(responseHeaders, "x-ratelimit-remaining-tokens", out var remainingTokens) && int.TryParse(remainingTokens, out var remainingTokensInt))
        {
            rateLimitInfo.RemainingTokens = remainingTokensInt;
            hasRateLimitData = true;
        }

        if (TryGetHeaderValue(responseHeaders, "x-ratelimit-limit-tokens", out var limitTokens) && int.TryParse(limitTokens, out var limitTokensInt))
        {
            rateLimitInfo.LimitTokens = limitTokensInt;
            hasRateLimitData = true;
        }

        return hasRateLimitData ? rateLimitInfo : null;
    }

    private bool TryGetHeaderValue(Dictionary<string, string[]> headers, string headerName, out string? value)
    {
        value = null;
        
        // Try case-insensitive lookup
        var key = headers.Keys.FirstOrDefault(k => k.Equals(headerName, StringComparison.OrdinalIgnoreCase));
        if (key != null && headers.TryGetValue(key, out var values) && values?.Length > 0)
        {
            value = values[0];
            return true;
        }

        return false;
    }

    private class OpenAIResponse
    {
        public string? Id { get; set; }
        public string? Object { get; set; }
        public long? Created { get; set; }
        public string? Model { get; set; }
        
        [JsonPropertyName("system_fingerprint")]
        public string? SystemFingerprint { get; set; }
        
        public List<Choice>? Choices { get; set; }
        public OpenAIUsage? Usage { get; set; }
    }

    private class Choice
    {
        public int? Index { get; set; }
        
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
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
    }

    private class PromptTokensDetails
    {
        [JsonPropertyName("cached_tokens")]
        public int? CachedTokens { get; set; }
    }
}