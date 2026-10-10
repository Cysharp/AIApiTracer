using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AIApiTracer.Models;

namespace AIApiTracer.Services.Metadata;

/// <summary>
/// Extracts AI metadata from Gemini API responses
/// </summary>
public class GeminiMetadataExtractor : BaseAiMetadataExtractor
{
    private static readonly Regex ModelRegex = new(@"models/([^/:]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override bool CanExtract(string targetUrl)
    {
        return targetUrl.Contains("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase) ||
               targetUrl.Contains("aiplatform.googleapis.com", StringComparison.OrdinalIgnoreCase);
    }

    public override AiMetadata? ExtractMetadata(string? targetUrl, string? requestBody, string? responseBody, Dictionary<string, string[]> responseHeaders)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        var response = TryDeserialize<GeminiResponse>(responseBody);
        if (response == null)
            return null;

        var metadata = new AiMetadata();

        // Extract model from URL if possible
        if (!string.IsNullOrEmpty(targetUrl))
        {
            var match = ModelRegex.Match(targetUrl);
            if (match.Success)
            {
                metadata.Model = match.Groups[1].Value;
            }
        }

        // If not in URL, check if it's in the request body (sometimes present in Vertex AI)
        if (string.IsNullOrEmpty(metadata.Model) && !string.IsNullOrEmpty(requestBody))
        {
            try
            {
                using var doc = JsonDocument.Parse(requestBody);
                if (doc.RootElement.TryGetProperty("model", out var modelProp))
                {
                    metadata.Model = modelProp.GetString();
                }
            }
            catch { /* Ignore */ }
        }

        if (response.UsageMetadata != null)
        {
            metadata.Usage = new TokenUsage
            {
                InputTokens = response.UsageMetadata.PromptTokenCount,
                OutputTokens = response.UsageMetadata.CandidatesTokenCount,
                TotalTokens = response.UsageMetadata.TotalTokenCount
            };
        }

        // Add finish reason from first candidate if available
        if (response.Candidates?.Count > 0)
        {
            var finishReason = response.Candidates[0].FinishReason;
            if (!string.IsNullOrEmpty(finishReason))
            {
                metadata.Extra["finish_reason"] = finishReason;
            }
        }

        return metadata;
    }

    private class GeminiResponse
    {
        public List<Candidate>? Candidates { get; set; }
        
        [JsonPropertyName("usageMetadata")]
        public GeminiUsage? UsageMetadata { get; set; }
    }

    private class Candidate
    {
        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }
        public int? Index { get; set; }
    }

    private class GeminiUsage
    {
        [JsonPropertyName("promptTokenCount")]
        public int? PromptTokenCount { get; set; }
        
        [JsonPropertyName("candidatesTokenCount")]
        public int? CandidatesTokenCount { get; set; }
        
        [JsonPropertyName("totalTokenCount")]
        public int? TotalTokenCount { get; set; }
    }
}
