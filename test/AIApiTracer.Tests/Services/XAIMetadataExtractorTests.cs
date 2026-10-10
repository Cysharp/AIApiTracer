using AIApiTracer.Services;
using AIApiTracer.Services.Metadata;
using AIApiTracer.Services.Streaming;
using Xunit;

namespace AIApiTracer.Tests.Services;

public class XAIMetadataExtractorTests
{
    private readonly XAIMetadataExtractor _extractor = new();

    [Fact]
    public void CanExtract_WithXAIUrl_ReturnsTrue()
    {
        // Arrange & Act
        var result = _extractor.CanExtract("https://api.x.ai/v1/chat/completions");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanExtract_WithNonXAIUrl_ReturnsFalse()
    {
        // Arrange & Act
        var result = _extractor.CanExtract("https://api.openai.com/v1/chat/completions");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ExtractMetadata_WithValidResponse_ExtractsModelAndUsage()
    {
        // Arrange
        var responseBody = """
        {
            "id": "cmpl-uqkvlQyYK7bGYrRHQ0eXlWi7",
            "object": "chat.completion",
            "created": 1677652288,
            "model": "grok-beta",
            "system_fingerprint": "fp_12345",
            "choices": [{
                "index": 0,
                "message": {
                    "role": "assistant",
                    "content": "Hello! How can I help you today?"
                },
                "finish_reason": "stop"
            }],
            "usage": {
                "prompt_tokens": 15,
                "completion_tokens": 8,
                "total_tokens": 23
            }
        }
        """;

        // Act
        var metadata = _extractor.ExtractMetadata(null, null, responseBody, new Dictionary<string, string[]>());

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("grok-beta", metadata.Model);
        Assert.NotNull(metadata.Usage);
        Assert.Equal(15, metadata.Usage.InputTokens);
        Assert.Equal(8, metadata.Usage.OutputTokens);
        Assert.Equal(23, metadata.Usage.TotalTokens);
        Assert.Equal("fp_12345", metadata.Extra["system_fingerprint"]);
        Assert.Equal("stop", metadata.Extra["finish_reason"]);
    }

    [Fact]
    public void ExtractMetadata_WithMissingUsage_StillExtractsModel()
    {
        // Arrange
        var responseBody = """
        {
            "id": "cmpl-123",
            "object": "chat.completion",
            "created": 1677652288,
            "model": "grok-beta",
            "choices": [{
                "index": 0,
                "message": {
                    "role": "assistant",
                    "content": "Hello!"
                },
                "finish_reason": "length"
            }]
        }
        """;

        // Act
        var metadata = _extractor.ExtractMetadata(null, null, responseBody, new Dictionary<string, string[]>());

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("grok-beta", metadata.Model);
        Assert.Null(metadata.Usage);
        Assert.True(metadata.Extra.ContainsKey("finish_reason"));
        Assert.Equal("length", metadata.Extra["finish_reason"]);
    }

    [Fact]
    public void ExtractMetadata_WithEmptyResponse_ReturnsNull()
    {
        // Arrange & Act
        var metadata = _extractor.ExtractMetadata(null, null, "", new Dictionary<string, string[]>());

        // Assert
        Assert.Null(metadata);
    }
}