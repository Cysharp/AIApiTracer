using AIApiTracer.Services;
using AIApiTracer.Services.Metadata;
using AIApiTracer.Services.Streaming;
using Xunit;

namespace AIApiTracer.Tests.Services;

public class AnthropicMetadataExtractorTests
{
    private readonly AnthropicMetadataExtractor _extractor = new();

    [Fact]
    public void CanExtract_WithAnthropicUrl_ReturnsTrue()
    {
        // Arrange & Act
        var result = _extractor.CanExtract("https://api.anthropic.com/v1/messages");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanExtract_WithNonAnthropicUrl_ReturnsFalse()
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
            "id": "msg_01XFDUDYJgAACzvnptvVoYEL",
            "type": "message",
            "role": "assistant",
            "model": "claude-3-5-sonnet-20241022",
            "content": [{"type": "text", "text": "Hello!"}],
            "stop_reason": "end_turn",
            "stop_sequence": null,
            "usage": {
                "input_tokens": 12,
                "output_tokens": 6
            }
        }
        """;

        // Act
        var metadata = _extractor.ExtractMetadata(null, null, responseBody, new Dictionary<string, string[]>());

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("claude-3-5-sonnet-20241022", metadata.Model);
        Assert.NotNull(metadata.Usage);
        Assert.Equal(12, metadata.Usage.InputTokens);
        Assert.Equal(6, metadata.Usage.OutputTokens);
        Assert.Equal(18, metadata.Usage.TotalTokens);
        Assert.Equal("end_turn", metadata.Extra["stop_reason"]);
    }

    [Fact]
    public void ExtractMetadata_WithCachedTokens_ExtractsCacheInfo()
    {
        // Arrange
        var responseBody = """
        {
            "id": "msg_01XFDUDYJgAACzvnptvVoYEL",
            "type": "message",
            "role": "assistant",
            "model": "claude-3-5-sonnet-20241022",
            "content": [{"type": "text", "text": "Hello!"}],
            "stop_reason": "end_turn",
            "usage": {
                "input_tokens": 1000,
                "output_tokens": 100,
                "cache_creation_input_tokens": 200,
                "cache_read_input_tokens": 800
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
        Assert.Equal(800, metadata.Usage.InputTokensCached); // cache_read_input_tokens
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

    [Fact]
    public void ExtractMetadata_WithEmptyResponse_ReturnsNull()
    {
        // Arrange & Act
        var metadata = _extractor.ExtractMetadata(null, null, "", new Dictionary<string, string[]>());

        // Assert
        Assert.Null(metadata);
    }
}