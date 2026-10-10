using AIApiTracer.Services.MessageParsing;
using Xunit;

namespace AIApiTracer.Tests.Services.MessageParsing;

public class MessageParserFactoryTests
{
    private readonly MessageParserFactory _factory = new(new IMessageParser[] 
    { 
        new OpenAIMessageParser(), 
        new AnthropicMessageParser(),
        new GeminiMessageParser()
    });

    [Theory]
    [InlineData("https://api.openai.com/v1/chat/completions", typeof(OpenAIMessageParser))]
    [InlineData("https://myresource.openai.azure.com/openai/deployments/gpt-4", typeof(OpenAIMessageParser))]
    [InlineData("https://api.anthropic.com/v1/messages", typeof(AnthropicMessageParser))]
    [InlineData("https://api.x.ai/v1/chat/completions", typeof(OpenAIMessageParser))]
    [InlineData("https://custom.api.invalid/v1/chat/completions", typeof(OpenAIMessageParser))]
    [InlineData("https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent", typeof(GeminiMessageParser))]
    public void GetParser_WithUrl_ReturnsCorrectParser(string url, Type expectedParserType)
    {
        // Act
        var parser = _factory.GetParser(url);

        // Assert
        Assert.NotNull(parser);
        Assert.IsType(expectedParserType, parser);
    }

    [Theory]
    [InlineData(EndpointType.OpenAI, typeof(OpenAIMessageParser))]
    [InlineData(EndpointType.AzureOpenAI, typeof(OpenAIMessageParser))]
    [InlineData(EndpointType.xAI, typeof(OpenAIMessageParser))]
    [InlineData(EndpointType.OpenAICompat, typeof(OpenAIMessageParser))]
    [InlineData(EndpointType.Anthropic, typeof(AnthropicMessageParser))]
    [InlineData(EndpointType.Gemini, typeof(GeminiMessageParser))]
    public void GetParser_WithEndpointType_ReturnsCorrectParser(EndpointType endpointType, Type expectedParserType)
    {
        // Act
        var parser = _factory.GetParser(endpointType);

        // Assert
        Assert.NotNull(parser);
        Assert.IsType(expectedParserType, parser);
    }

    [Fact]
    public void GetParser_WithUnknownEndpoint_ReturnsNull()
    {
        // Act
        var parser = _factory.GetParser(EndpointType.Unknown);

        // Assert
        Assert.Null(parser);
    }

    [Fact]
    public void GetParser_WithUnknownUrl_ReturnsNull()
    {
        // Act
        var parser = _factory.GetParser("https://unknown.api.invalid/some/endpoint");

        // Assert
        Assert.Null(parser);
    }
}