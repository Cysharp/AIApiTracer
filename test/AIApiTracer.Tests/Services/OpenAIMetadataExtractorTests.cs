using AIApiTracer.Services;
using AIApiTracer.Services.Metadata;
using AIApiTracer.Services.Streaming;
using Xunit;

namespace AIApiTracer.Tests.Services;

public class OpenAIMetadataExtractorTests
{
    private readonly OpenAIMetadataExtractor _extractor = new();

    [Theory]
    [InlineData("https://api.openai.com/v1/chat/completions", true)]
    [InlineData("https://myresource.openai.azure.com/openai/deployments/gpt-4", true)]
    [InlineData("https://api.anthropic.com/v1/messages", false)]
    public void CanExtract_WithVariousUrls_ReturnsExpectedResult(string url, bool expected)
    {
        // Act
        var result = _extractor.CanExtract(url);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtractMetadata_WithValidResponse_ExtractsModelAndUsage()
    {
        // Arrange
        var responseBody = """
        {
            "id": "chatcmpl-123",
            "object": "chat.completion",
            "created": 1677652288,
            "model": "gpt-4-0613",
            "system_fingerprint": "fp_44709d6fcb",
            "choices": [{
                "index": 0,
                "message": {
                    "role": "assistant",
                    "content": "Hello!"
                },
                "finish_reason": "stop"
            }],
            "usage": {
                "prompt_tokens": 9,
                "completion_tokens": 12,
                "total_tokens": 21
            }
        }
        """;

        // Act
        var metadata = _extractor.ExtractMetadata(null, null, responseBody, new Dictionary<string, string[]>());

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("gpt-4-0613", metadata.Model);
        Assert.NotNull(metadata.Usage);
        Assert.Equal(9, metadata.Usage.InputTokens);
        Assert.Equal(12, metadata.Usage.OutputTokens);
        Assert.Equal(21, metadata.Usage.TotalTokens);
        Assert.Equal("fp_44709d6fcb", metadata.Extra["system_fingerprint"]);
        Assert.Equal("stop", metadata.Extra["finish_reason"]);
    }

    [Fact]
    public void ExtractMetadata_WithCachedTokens_ExtractsCacheInfo()
    {
        // Arrange
        var responseBody = """
        {
            "id": "chatcmpl-123",
            "object": "chat.completion",
            "created": 1677652288,
            "model": "gpt-4o",
            "choices": [{
                "index": 0,
                "message": {
                    "role": "assistant",
                    "content": "Hello!"
                },
                "finish_reason": "stop"
            }],
            "usage": {
                "prompt_tokens": 1000,
                "completion_tokens": 100,
                "total_tokens": 1100,
                "prompt_tokens_details": {
                    "cached_tokens": 800
                }
            }
        }
        """;

        // Act
        var metadata = _extractor.ExtractMetadata(null, null, responseBody, new Dictionary<string, string[]>());

        // Assert
        Assert.NotNull(metadata);
        Assert.NotNull(metadata.Usage);
        Assert.Equal(1000, metadata.Usage.InputTokens);
        Assert.Equal(100, metadata.Usage.OutputTokens);
        Assert.Equal(1100, metadata.Usage.TotalTokens);
        Assert.Equal(800, metadata.Usage.CachedTokens);
        Assert.Equal(800, metadata.Usage.InputTokensCached);
    }

    [Fact]
    public void ExtractMetadata_WithStreamingResponse_HandlesPartialData()
    {
        // Arrange
        var responseBody = """
        {
            "id": "chatcmpl-123",
            "object": "chat.completion",
            "created": 1677652288,
            "model": "gpt-3.5-turbo",
            "choices": [{
                "index": 0,
                "delta": {},
                "finish_reason": null
            }]
        }
        """;

        // Act
        var metadata = _extractor.ExtractMetadata(null, null, responseBody, new Dictionary<string, string[]>());

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("gpt-3.5-turbo", metadata.Model);
        Assert.Null(metadata.Usage); // No usage in streaming responses
    }

    [Fact]
    public void ExtractMetadata_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        var responseBody = "not valid json";

        // Act
        var metadata = _extractor.ExtractMetadata(null, null, responseBody, new Dictionary<string, string[]>());

        // Assert
        Assert.Null(metadata);
    }
}