using AIApiTracer.Models;
using AIApiTracer.Services;
using AIApiTracer.Services.Metadata;
using AIApiTracer.Services.Streaming;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace AIApiTracer.Middleware;

public class ApiTraceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IApiTraceService _apiTraceService;
    private readonly ISseParserFactory _sseParserFactory;
    private readonly IHeaderMaskingService _headerMaskingService;
    private readonly IAiMetadataExtractorFactory _aiMetadataExtractorFactory;
    private readonly ILogger<ApiTraceMiddleware> _logger;

    public ApiTraceMiddleware(
        RequestDelegate next, 
        IApiTraceService apiTraceService, 
        ISseParserFactory sseParserFactory,
        IHeaderMaskingService headerMaskingService,
        IAiMetadataExtractorFactory aiMetadataExtractorFactory,
        ILogger<ApiTraceMiddleware> logger)
    {
        _next = next;
        _apiTraceService = apiTraceService;
        _sseParserFactory = sseParserFactory;
        _headerMaskingService = headerMaskingService;
        _aiMetadataExtractorFactory = aiMetadataExtractorFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only trace API endpoints
        if (!context.Request.Path.StartsWithSegments("/endpoint"))
        {
            await _next(context);
            return;
        }

        var startTimestamp = Stopwatch.GetTimestamp();
        
        // Identify endpoint type from URL path
        var endpointType = GetEndpointType(context.Request.Path);
        if (!string.IsNullOrEmpty(endpointType))
        {
            context.Items["ApiTrace_EndpointType"] = endpointType;
        }

        // Create trace record
        var traceRecord = new ApiTraceRecord
        {
            Method = context.Request.Method,
            Url = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}",
            Request = new RequestData
            {
                Headers = _headerMaskingService.MaskSensitiveHeaders(
                    context.Request.Headers.ToDictionary(
                        h => h.Key,
                        h => h.Value.Select(v => v ?? string.Empty).ToArray()
                    )
                )
            }
        };

        // Capture request body
        if (context.Request.ContentLength > 0)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            traceRecord.Request.Body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        // Store trace record
        context.Items["ApiTrace_Record"] = traceRecord;

        // Store original response body stream
        var originalBodyStream = context.Response.Body;
        
        // Create a temporary capturing stream without content encoding (we'll update it later)
        var capturingStream = new MemoryStream();
        context.Response.Body = capturingStream;

        try
        {
            // Call the next middleware
            await _next(context);
            
            // After the response is complete, capture all metadata
            traceRecord.StatusCode = context.Response.StatusCode;
            traceRecord.Response.Headers = _headerMaskingService.MaskSensitiveHeaders(
                context.Response.Headers.ToDictionary(
                    h => h.Key,
                    h => h.Value.Select(v => v ?? string.Empty).ToArray()
                )
            );

            // Get target URL if available
            if (context.Items.TryGetValue("ApiTrace_TargetUrl", out var targetUrlObj) && 
                targetUrlObj is string targetUrl)
            {
                traceRecord.TargetUrl = targetUrl;
            }

            // Get Content-Encoding header for decompression
            var contentEncoding = context.Response.Headers.ContentEncoding.FirstOrDefault();
            
            // Check if response is SSE and we have a parser for it
            var contentType = context.Response.Headers.ContentType.FirstOrDefault();
            var isSSE = contentType?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) ?? false;
            
            if (isSSE)
            {
                // First, capture raw SSE data
                capturingStream.Position = 0;
                var rawSseData = await GetResponseBodyText(capturingStream, contentEncoding);
                traceRecord.Response.RawSseData = rawSseData;
                
                // Get endpoint type from context
                var endpointTypeFromContext = context.Items.TryGetValue("ApiTrace_EndpointType", out var endpointTypeObj) 
                    ? endpointTypeObj?.ToString() 
                    : null;
                    
                var sseParser = GetSseParserByEndpointType(endpointTypeFromContext);
                if (sseParser != null)
                {
                    // Parse SSE stream and merge into single JSON
                    capturingStream.Position = 0;
                    try
                    {
                        traceRecord.Response.Body = await sseParser.ParseSseStreamAsync(capturingStream);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse SSE stream, using raw text");
                        traceRecord.Response.Body = rawSseData;
                    }
                }
                else
                {
                    // No parser available, use raw text
                    traceRecord.Response.Body = rawSseData;
                }
            }
            else
            {
                // Non-SSE response, process normally
                capturingStream.Position = 0;
                traceRecord.Response.Body = await GetResponseBodyText(capturingStream, contentEncoding);
            }
            
            // Copy the captured stream to the original response stream
            capturingStream.Position = 0;
            await capturingStream.CopyToAsync(originalBodyStream);

            // Final duration update
            var finalElapsed = Stopwatch.GetElapsedTime(startTimestamp);
            traceRecord.Duration = (long)finalElapsed.TotalMilliseconds;

            // Extract AI metadata if available
            var endpointTypeForMetadata = context.Items.TryGetValue("ApiTrace_EndpointType", out var endpointTypeObjForMetadata) 
                ? endpointTypeObjForMetadata?.ToString() 
                : null;
                
            var metadataExtractor = GetMetadataExtractorByEndpointType(endpointTypeForMetadata);
            if (metadataExtractor != null)
            {
                try
                {
                    traceRecord.AiMetadata = metadataExtractor.ExtractMetadata(
                        traceRecord.TargetUrl,
                        traceRecord.Request.Body,
                        traceRecord.Response.Body,
                        traceRecord.Response.Headers
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract AI metadata");
                }
            }

            // Add trace to service
            _apiTraceService.AddTrace(traceRecord);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in API trace middleware");
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
            capturingStream?.Dispose();
        }
    }

    private async Task<string> GetResponseBodyText(Stream stream, string? contentEncoding)
    {
        const int maxSize = 1024 * 1024; // 1MB limit
        
        try
        {
            // Limit the size we read
            if (stream.Length > maxSize)
            {
                var buffer = new byte[maxSize];
                await stream.ReadExactlyAsync(buffer.AsMemory(0, maxSize));
                stream = new MemoryStream(buffer);
            }

            // Decompress if content is encoded
            Stream readStream = stream;
            
            if (!string.IsNullOrEmpty(contentEncoding))
            {
                switch (contentEncoding.ToLowerInvariant())
                {
                    case "gzip":
                        readStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
                        break;
                    case "deflate":
                        readStream = new DeflateStream(stream, CompressionMode.Decompress, leaveOpen: true);
                        break;
                    case "br":
                        readStream = new BrotliStream(stream, CompressionMode.Decompress, leaveOpen: true);
                        break;
                }
            }
            
            using var reader = new StreamReader(readStream, Encoding.UTF8, leaveOpen: true);
            var text = await reader.ReadToEndAsync();
            
            if (stream.Length > maxSize)
            {
                text += "\n\n[Response body truncated at 1MB]";
            }

            return text;
        }
        catch (Exception ex)
        {
            // If decompression fails, try to read as plain text
            try
            {
                stream.Position = 0;
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                return $"[Error decompressing response: {ex.Message}]\n\n" + await reader.ReadToEndAsync();
            }
            catch
            {
                return $"[Error reading response body: {ex.Message}]";
            }
        }
    }
    
    private string? GetEndpointType(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant();
        if (string.IsNullOrEmpty(pathValue))
            return null;
            
        if (pathValue.Contains("/endpoint/openai/"))
            return "OpenAI";
        else if (pathValue.Contains("/endpoint/anthropic/"))
            return "Anthropic";
        else if (pathValue.Contains("/endpoint/aoai/"))
            return "AzureOpenAI";
        else if (pathValue.Contains("/endpoint/x/"))
            return "xAI";
        else if (pathValue.Contains("/endpoint/openai-compat/"))
            return "OpenAICompat";
        else if (pathValue.Contains("/endpoint/gemini/"))
            return "Gemini";
            
        return null;
    }
    
    private ISseParser? GetSseParserByEndpointType(string? endpointType)
    {
        if (string.IsNullOrEmpty(endpointType))
            return null;
            
        return endpointType switch
        {
            "OpenAI" => _sseParserFactory.GetAllParsers().FirstOrDefault(p => p.GetType().Name == "OpenAISseParser"),
            "AzureOpenAI" => _sseParserFactory.GetAllParsers().FirstOrDefault(p => p.GetType().Name == "OpenAISseParser"),
            "xAI" => _sseParserFactory.GetAllParsers().FirstOrDefault(p => p.GetType().Name == "OpenAISseParser"),
            "Anthropic" => _sseParserFactory.GetAllParsers().FirstOrDefault(p => p.GetType().Name == "AnthropicSseParser"),
            "Gemini" => _sseParserFactory.GetAllParsers().FirstOrDefault(p => p.GetType().Name == "OpenAISseParser"),
            "OpenAICompat" => _sseParserFactory.GetAllParsers().FirstOrDefault(p => p.GetType().Name == "OpenAICompatSseParser"),
            _ => null
        };
    }
    
    private IAiMetadataExtractor? GetMetadataExtractorByEndpointType(string? endpointType)
    {
        if (string.IsNullOrEmpty(endpointType))
            return null;
            
        return endpointType switch
        {
            "OpenAI" => _aiMetadataExtractorFactory.GetAllExtractors().FirstOrDefault(e => e.GetType().Name == "OpenAIMetadataExtractor"),
            "AzureOpenAI" => _aiMetadataExtractorFactory.GetAllExtractors().FirstOrDefault(e => e.GetType().Name == "OpenAIMetadataExtractor"),
            "xAI" => _aiMetadataExtractorFactory.GetAllExtractors().FirstOrDefault(e => e.GetType().Name == "XAIMetadataExtractor"),
            "Anthropic" => _aiMetadataExtractorFactory.GetAllExtractors().FirstOrDefault(e => e.GetType().Name == "AnthropicMetadataExtractor"),
            "Gemini" => _aiMetadataExtractorFactory.GetAllExtractors().FirstOrDefault(e => e.GetType().Name == "GeminiMetadataExtractor"),
            "OpenAICompat" => _aiMetadataExtractorFactory.GetAllExtractors().FirstOrDefault(e => e.GetType().Name == "OpenAICompatMetadataExtractor"),
            _ => null
        };
    }
}