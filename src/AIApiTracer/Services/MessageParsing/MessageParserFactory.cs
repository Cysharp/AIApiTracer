namespace AIApiTracer.Services.MessageParsing;

/// <summary>
/// Factory for creating message parsers based on endpoint type
/// </summary>
public class MessageParserFactory : IMessageParserFactory
{
    private readonly IEnumerable<IMessageParser> _parsers;

    public MessageParserFactory(IEnumerable<IMessageParser> parsers)
    {
        _parsers = parsers;
    }

    public IMessageParser? GetParser(EndpointType endpointType)
    {
        return _parsers.FirstOrDefault(p => p.CanParse(endpointType));
    }

    public IMessageParser? GetParser(string targetUrl)
    {
        var endpointType = DetermineEndpointType(targetUrl);
        return GetParser(endpointType);
    }

    private EndpointType DetermineEndpointType(string targetUrl)
    {
        if (targetUrl.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase))
            return EndpointType.OpenAI;
        
        if (targetUrl.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase))
            return EndpointType.AzureOpenAI;
        
        if (targetUrl.Contains("api.anthropic.com", StringComparison.OrdinalIgnoreCase))
            return EndpointType.Anthropic;
        
        if (targetUrl.Contains("api.x.ai", StringComparison.OrdinalIgnoreCase))
            return EndpointType.xAI;

        if (targetUrl.Contains("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase) ||
            targetUrl.Contains("aiplatform.googleapis.com", StringComparison.OrdinalIgnoreCase))
            return EndpointType.Gemini;
        
        // Check if it's an OpenAI-compatible endpoint
        if (targetUrl.Contains("/v1/chat/completions", StringComparison.OrdinalIgnoreCase) ||
            targetUrl.Contains("/v1/completions", StringComparison.OrdinalIgnoreCase))
            return EndpointType.OpenAICompat;
        
        return EndpointType.Unknown;
    }
}

/// <summary>
/// Interface for message parser factory
/// </summary>
public interface IMessageParserFactory
{
    IMessageParser? GetParser(EndpointType endpointType);
    IMessageParser? GetParser(string targetUrl);
}