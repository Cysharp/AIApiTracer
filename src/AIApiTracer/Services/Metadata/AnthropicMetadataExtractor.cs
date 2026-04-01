using System.Text.Json;
using System.Text.Json.Serialization;
using AIApiTracer.Models;

namespace AIApiTracer.Services.Metadata;

/// <summary>
/// Extracts AI metadata from Anthropic API responses
/// </summary>
public class AnthropicMetadataExtractor : BaseAiMetadataExtractor
{
    public override bool CanExtract(string targetUrl)
    {
        return targetUrl.Contains("api.anthropic.com", StringComparison.OrdinalIgnoreCase);
    }

    public override AiMetadata? ExtractMetadata(string? targetUrl, string? requestBody, string? responseBody, Dictionary<string, string[]> responseHeaders)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        var response = TryDeserialize<AnthropicResponse>(responseBody);
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
                InputTokens = response.Usage.InputTokens,
                OutputTokens = response.Usage.OutputTokens,
                InputTokensCached = response.Usage.CacheReadInputTokens ?? response.Usage.CacheCreationInputTokens,
                CacheCreationInputTokens = response.Usage.CacheCreationInputTokens,
                CacheReadInputTokens = response.Usage.CacheReadInputTokens
            };

            // Calculate total tokens
            if (metadata.Usage.InputTokens.HasValue && metadata.Usage.OutputTokens.HasValue)
            {
                metadata.Usage.TotalTokens = metadata.Usage.InputTokens.Value + metadata.Usage.OutputTokens.Value;
            }
        }

        // Add stop reason to extra metadata if present
        if (!string.IsNullOrEmpty(response.StopReason))
        {
            metadata.Extra["stop_reason"] = response.StopReason;
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

        // Anthropic uses anthropic-ratelimit-* headers
        if (TryGetHeaderValue(responseHeaders, "anthropic-ratelimit-requests-remaining", out var remainingRequests) && int.TryParse(remainingRequests, out var remainingRequestsInt))
        {
            rateLimitInfo.RemainingRequests = remainingRequestsInt;
            hasRateLimitData = true;
        }

        if (TryGetHeaderValue(responseHeaders, "anthropic-ratelimit-requests-limit", out var limitRequests) && int.TryParse(limitRequests, out var limitRequestsInt))
        {
            rateLimitInfo.LimitRequests = limitRequestsInt;
            hasRateLimitData = true;
        }

        if (TryGetHeaderValue(responseHeaders, "anthropic-ratelimit-tokens-remaining", out var remainingTokens) && int.TryParse(remainingTokens, out var remainingTokensInt))
        {
            rateLimitInfo.RemainingTokens = remainingTokensInt;
            hasRateLimitData = true;
        }

        if (TryGetHeaderValue(responseHeaders, "anthropic-ratelimit-tokens-limit", out var limitTokens) && int.TryParse(limitTokens, out var limitTokensInt))
        {
            rateLimitInfo.LimitTokens = limitTokensInt;
            hasRateLimitData = true;
        }

        // Extract reset timestamp for Anthropic
        if (TryGetHeaderValue(responseHeaders, "anthropic-ratelimit-unified-reset", out var resetTimestamp) && long.TryParse(resetTimestamp, out var resetTimestampLong))
        {
            rateLimitInfo.ResetTimestamp = resetTimestampLong;
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

    private class AnthropicResponse
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public string? Role { get; set; }
        public string? Model { get; set; }
        
        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }
        
        [JsonPropertyName("stop_sequence")]
        public string? StopSequence { get; set; }
        
        public AnthropicUsage? Usage { get; set; }
    }

    private class AnthropicUsage
    {
        [JsonPropertyName("input_tokens")]
        public int? InputTokens { get; set; }
        
        [JsonPropertyName("output_tokens")]
        public int? OutputTokens { get; set; }
        
        [JsonPropertyName("cache_creation_input_tokens")]
        public int? CacheCreationInputTokens { get; set; }
        
        [JsonPropertyName("cache_read_input_tokens")]
        public int? CacheReadInputTokens { get; set; }
    }
}