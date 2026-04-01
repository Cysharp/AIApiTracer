using AIApiTracer.Services;
using AIApiTracer.Services.Metadata;
using AIApiTracer.Services.Streaming;
using Xunit;

namespace AIApiTracer.Tests.Services;

public class AiMetadataExtractorFactoryTests
{
    [Fact]
    public void GetExtractor_WithAnthropicUrl_ReturnsAnthropicExtractor()
    {
        // Arrange
        var extractors = new IAiMetadataExtractor[]
        {
            new AnthropicMetadataExtractor(),
            new OpenAIMetadataExtractor(),
            new XAIMetadataExtractor(),
            new GeminiMetadataExtractor()
        };
        var factory = new AiMetadataExtractorFactory(extractors);

        // Act
        var extractor = factory.GetExtractor("https://api.anthropic.com/v1/messages");

        // Assert
        Assert.NotNull(extractor);
        Assert.IsType<AnthropicMetadataExtractor>(extractor);
    }

    [Fact]
    public void GetExtractor_WithGeminiUrl_ReturnsGeminiExtractor()
    {
        // Arrange
        var extractors = new IAiMetadataExtractor[]
        {
            new AnthropicMetadataExtractor(),
            new OpenAIMetadataExtractor(),
            new XAIMetadataExtractor(),
            new GeminiMetadataExtractor()
        };
        var factory = new AiMetadataExtractorFactory(extractors);

        // Act
        var extractor = factory.GetExtractor("https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent");

        // Assert
        Assert.NotNull(extractor);
        Assert.IsType<GeminiMetadataExtractor>(extractor);
    }

    [Fact]
    public void GetExtractor_WithOpenAIUrl_ReturnsOpenAIExtractor()
    {
        // Arrange
        var extractors = new IAiMetadataExtractor[]
        {
            new AnthropicMetadataExtractor(),
            new OpenAIMetadataExtractor(),
            new XAIMetadataExtractor(),
            new GeminiMetadataExtractor()
        };
        var factory = new AiMetadataExtractorFactory(extractors);

        // Act
        var extractor = factory.GetExtractor("https://api.openai.com/v1/chat/completions");

        // Assert
        Assert.NotNull(extractor);
        Assert.IsType<OpenAIMetadataExtractor>(extractor);
    }

    [Fact]
    public void GetExtractor_WithAzureOpenAIUrl_ReturnsOpenAIExtractor()
    {
        // Arrange
        var extractors = new IAiMetadataExtractor[]
        {
            new AnthropicMetadataExtractor(),
            new OpenAIMetadataExtractor(),
            new XAIMetadataExtractor(),
            new GeminiMetadataExtractor()
        };
        var factory = new AiMetadataExtractorFactory(extractors);

        // Act
        var extractor = factory.GetExtractor("https://myresource.openai.azure.com/openai/deployments/gpt-4");

        // Assert
        Assert.NotNull(extractor);
        Assert.IsType<OpenAIMetadataExtractor>(extractor);
    }

    [Fact]
    public void GetExtractor_WithXAIUrl_ReturnsXAIExtractor()
    {
        // Arrange
        var extractors = new IAiMetadataExtractor[]
        {
            new AnthropicMetadataExtractor(),
            new OpenAIMetadataExtractor(),
            new XAIMetadataExtractor(),
            new GeminiMetadataExtractor()
        };
        var factory = new AiMetadataExtractorFactory(extractors);

        // Act
        var extractor = factory.GetExtractor("https://api.x.ai/v1/chat/completions");

        // Assert
        Assert.NotNull(extractor);
        Assert.IsType<XAIMetadataExtractor>(extractor);
    }

    [Fact]
    public void GetExtractor_WithUnknownUrl_ReturnsNull()
    {
        // Arrange
        var extractors = new IAiMetadataExtractor[]
        {
            new AnthropicMetadataExtractor(),
            new OpenAIMetadataExtractor(),
            new XAIMetadataExtractor(),
            new GeminiMetadataExtractor()
        };
        var factory = new AiMetadataExtractorFactory(extractors);

        // Act
        var extractor = factory.GetExtractor("https://api.example.invalid/v1/chat");

        // Assert
        Assert.Null(extractor);
    }

    [Fact]
    public void GetExtractor_WithEmptyUrl_ReturnsNull()
    {
        // Arrange
        var extractors = new IAiMetadataExtractor[]
        {
            new AnthropicMetadataExtractor(),
            new OpenAIMetadataExtractor(),
            new XAIMetadataExtractor(),
            new GeminiMetadataExtractor()
        };
        var factory = new AiMetadataExtractorFactory(extractors);

        // Act
        var extractor = factory.GetExtractor("");

        // Assert
        Assert.Null(extractor);
    }

    [Fact]
    public void GetExtractor_WithNullUrl_ReturnsNull()
    {
        // Arrange
        var extractors = new IAiMetadataExtractor[]
        {
            new AnthropicMetadataExtractor(),
            new OpenAIMetadataExtractor(),
            new XAIMetadataExtractor(),
            new GeminiMetadataExtractor()
        };
        var factory = new AiMetadataExtractorFactory(extractors);

        // Act
        var extractor = factory.GetExtractor(null!);

        // Assert
        Assert.Null(extractor);
    }
}