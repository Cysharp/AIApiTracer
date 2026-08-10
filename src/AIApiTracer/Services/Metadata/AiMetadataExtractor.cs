using System.Text.Json;
using AIApiTracer.Models;

namespace AIApiTracer.Services.Metadata;

/// <summary>
/// Interface for extracting AI metadata from API responses
/// </summary>
public interface IAiMetadataExtractor
{
    /// <summary>
    /// Determines if this extractor can handle the given endpoint
    /// </summary>
    bool CanExtract(string targetUrl);

    /// <summary>
    /// Extracts AI metadata from request and response
    /// </summary>
    AiMetadata? ExtractMetadata(string? targetUrl, string? requestBody, string? responseBody, Dictionary<string, string[]> responseHeaders);
}

/// <summary>
/// Base class for AI metadata extractors
/// </summary>
public abstract class BaseAiMetadataExtractor : IAiMetadataExtractor
{
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public abstract bool CanExtract(string targetUrl);
    public abstract AiMetadata? ExtractMetadata(string? targetUrl, string? requestBody, string? responseBody, Dictionary<string, string[]> responseHeaders);

    protected T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}