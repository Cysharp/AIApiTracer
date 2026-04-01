using AIApiTracer.Services.Metadata;
using Xunit;

namespace AIApiTracer.Tests.Services;

public class GeminiMetadataExtractorTests
{
    private readonly GeminiMetadataExtractor _extractor = new();

    [Theory]
    [InlineData("https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent", true)]
    [InlineData("https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/publishers/google/models/gemini-1.5-flash:streamGenerateContent", true)]
    [InlineData("https://api.openai.com/v1/chat/completions", false)]
    public void CanExtract_WithVariousUrls_ReturnsExpectedResult(string url, bool expected)
    {
        // Act
        var result = _extractor.CanExtract(url);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtractMetadata_WithValidResponse_ExtractsUsageAndModelFromUrl()
    {
        // Arrange
        var targetUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent";
        var responseBody = """
        {
            "candidates": [
                {
                    "content": {
                        "parts": [
                            {
                                "text": "Hello!"
                            }
                        ],
                        "role": "model"
                    },
                    "finishReason": "STOP",
                    "index": 0
                }
            ],
            "usageMetadata": {
                "promptTokenCount": 5,
                "candidatesTokenCount": 2,
                "totalTokenCount": 7
            }
        }
        """;

        // Act
        var metadata = _extractor.ExtractMetadata(targetUrl, null, responseBody, new Dictionary<string, string[]>());

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("gemini-1.5-pro", metadata.Model);
        Assert.NotNull(metadata.Usage);
        Assert.Equal(5, metadata.Usage.InputTokens);
        Assert.Equal(2, metadata.Usage.OutputTokens);
        Assert.Equal(7, metadata.Usage.TotalTokens);
        Assert.Equal("STOP", metadata.Extra["finish_reason"]);
    }

    [Fact]
    public void ExtractMetadata_WithModelInRequestBody_ExtractsModelFromRequest()
    {
        // Arrange
        var requestBody = """
        {
            "model": "gemini-1.5-flash",
            "contents": []
        }
        """;
        var responseBody = "{}";

        // Act
        var metadata = _extractor.ExtractMetadata(null, requestBody, responseBody, new Dictionary<string, string[]>());

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("gemini-1.5-flash", metadata.Model);
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
