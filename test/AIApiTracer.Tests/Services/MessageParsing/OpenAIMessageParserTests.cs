using AIApiTracer.Services.MessageParsing;
using Xunit;

namespace AIApiTracer.Tests.Services.MessageParsing;

public class OpenAIMessageParserTests
{
    private readonly OpenAIMessageParser _parser = new();

    [Theory]
    [InlineData(EndpointType.OpenAI, true)]
    [InlineData(EndpointType.AzureOpenAI, true)]
    [InlineData(EndpointType.xAI, true)]
    [InlineData(EndpointType.OpenAICompat, true)]
    [InlineData(EndpointType.Anthropic, false)]
    [InlineData(EndpointType.Unknown, false)]
    public void CanParse_WithVariousEndpoints_ReturnsExpectedResult(EndpointType endpointType, bool expected)
    {
        // Act
        var result = _parser.CanParse(endpointType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Parse_RequestWithSimpleMessages_ExtractsMessagesCorrectly()
    {
        // Arrange
        var json = """
        {
            "model": "gpt-4",
            "messages": [
                {"role": "system", "content": "You are a helpful assistant."},
                {"role": "user", "content": "Hello!"}
            ],
            "temperature": 0.7
        }
        """;

        // Act
        var result = _parser.Parse(json, isRequest: true);

        // Assert
        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("system", result.Messages[0].Role);
        Assert.Equal("You are a helpful assistant.", result.Messages[0].Content);
        Assert.Equal("user", result.Messages[1].Role);
        Assert.Equal("Hello!", result.Messages[1].Content);
        Assert.Contains("model", result.OtherData);
        Assert.Contains("temperature", result.OtherData);
    }

    [Fact]
    public void Parse_RequestWithMultimodalContent_ExtractsContentPartsCorrectly()
    {
        // Arrange
        var json = """
        {
            "model": "gpt-4-vision",
            "messages": [
                {
                    "role": "user",
                    "content": [
                        {"type": "text", "text": "What's in this image?"},
                        {"type": "image_url", "image_url": {"url": "https://example.invalid/image.jpg"}}
                    ]
                }
            ]
        }
        """;

        // Act
        var result = _parser.Parse(json, isRequest: true);

        // Assert
        Assert.Single(result.Messages);
        var message = result.Messages[0];
        Assert.Equal("user", message.Role);
        Assert.NotNull(message.ContentParts);
        Assert.Equal(2, message.ContentParts.Count);
        Assert.Equal("text", message.ContentParts[0].Type);
        Assert.Equal("What's in this image?", message.ContentParts[0].Text);
        Assert.Equal("image_url", message.ContentParts[1].Type);
        Assert.Equal("https://example.invalid/image.jpg", message.ContentParts[1].ImageUrl);
    }

    [Fact]
    public void Parse_RequestWithToolCalls_ExtractsToolCallsCorrectly()
    {
        // Arrange
        var json = """
        {
            "model": "gpt-4",
            "messages": [
                {
                    "role": "assistant",
                    "content": null,
                    "tool_calls": [
                        {
                            "id": "call_123",
                            "type": "function",
                            "function": {
                                "name": "get_weather",
                                "arguments": "{\"location\": \"Tokyo\"}"
                            }
                        }
                    ]
                }
            ]
        }
        """;

        // Act
        var result = _parser.Parse(json, isRequest: true);

        // Assert
        Assert.Single(result.Messages);
        Assert.Single(result.ToolCalls);
        var toolCall = result.ToolCalls[0];
        Assert.Equal("call_123", toolCall.Id);
        Assert.Equal("function", toolCall.Type);
        Assert.Equal("get_weather", toolCall.Name);
        Assert.NotNull(toolCall.Arguments);
    }

    [Fact]
    public void Parse_ResponseWithChoices_ExtractsMessagesCorrectly()
    {
        // Arrange
        var json = """
        {
            "id": "chatcmpl-123",
            "object": "chat.completion",
            "created": 1677652288,
            "model": "gpt-4",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": "Hello! How can I help you today?"
                    },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 10,
                "completion_tokens": 20,
                "total_tokens": 30
            }
        }
        """;

        // Act
        var result = _parser.Parse(json, isRequest: false);

        // Assert
        Assert.Single(result.Messages);
        var message = result.Messages[0];
        Assert.Equal("assistant", message.Role);
        Assert.Equal("Hello! How can I help you today?", message.Content);
        Assert.Contains("id", result.OtherData);
        Assert.Contains("model", result.OtherData);
        Assert.Contains("usage", result.OtherData);
    }

    [Fact]
    public void Parse_StreamingResponseWithDelta_ExtractsMessagesCorrectly()
    {
        // Arrange
        var json = """
        {
            "id": "chatcmpl-123",
            "object": "chat.completion.chunk",
            "created": 1677652288,
            "model": "gpt-4",
            "choices": [
                {
                    "index": 0,
                    "delta": {
                        "role": "assistant",
                        "content": "Hello"
                    },
                    "finish_reason": null
                }
            ]
        }
        """;

        // Act
        var result = _parser.Parse(json, isRequest: false);

        // Assert
        Assert.Single(result.Messages);
        var message = result.Messages[0];
        Assert.Equal("assistant", message.Role);
        Assert.Equal("Hello", message.Content);
    }

    [Fact]
    public void Parse_ResponseWithToolCalls_ExtractsToolCallsCorrectly()
    {
        // Arrange
        var json = """
        {
            "id": "chatcmpl-123",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": null,
                        "tool_calls": [
                            {
                                "id": "call_abc",
                                "type": "function",
                                "function": {
                                    "name": "calculate",
                                    "arguments": "{\"a\": 5, \"b\": 3}"
                                }
                            }
                        ]
                    },
                    "finish_reason": "tool_calls"
                }
            ]
        }
        """;

        // Act
        var result = _parser.Parse(json, isRequest: false);

        // Assert
        Assert.Single(result.Messages);
        Assert.Single(result.ToolCalls);
        var toolCall = result.ToolCalls[0];
        Assert.Equal("call_abc", toolCall.Id);
        Assert.Equal("calculate", toolCall.Name);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsEmptyResult()
    {
        // Arrange
        var json = "invalid json";

        // Act
        var result = _parser.Parse(json, isRequest: true);

        // Assert
        Assert.Empty(result.Messages);
        Assert.Empty(result.ToolCalls);
        Assert.Empty(result.OtherData);
    }

    [Fact]
    public void Parse_EmptyJson_ReturnsEmptyResult()
    {
        // Arrange
        var json = "{}";

        // Act
        var result = _parser.Parse(json, isRequest: true);

        // Assert
        Assert.Empty(result.Messages);
        Assert.Empty(result.ToolCalls);
        Assert.Empty(result.OtherData);
    }

    [Fact]
    public void Parse_xAIResponseWithCitationsAndUsage_ExtractsAllData()
    {
        // Arrange
        var json = """
        {
            "id": "455de541-db79-c000-dc90-ec10264c8f9f",
            "object": "chat.completion",
            "created": 1752222404,
            "model": "grok-3",
            "choices": [{
                "index": 0,
                "message": {
                    "role": "assistant",
                    "content": "Here is a digest of world news."
                },
                "finish_reason": "stop"
            }],
            "usage": {
                "prompt_tokens": 2657,
                "completion_tokens": 851,
                "total_tokens": 3508
            },
            "system_fingerprint": "fp_9ad1a16f77",
            "citations": [
                "https://www.npr.org/programs/all-things-considered/2025/07/09/all-things-considered-for-july-09-2025",
                "https://abcnews.go.com/International"
            ]
        }
        """;

        // Act
        var result = _parser.Parse(json, isRequest: false);

        // Assert
        Assert.Single(result.Messages);
        var message = result.Messages[0];
        Assert.Equal("assistant", message.Role);
        Assert.Equal("Here is a digest of world news.", message.Content);
        
        // Check that usage and citations are in OtherData
        Assert.Contains("usage", result.OtherData);
        Assert.Contains("citations", result.OtherData);
        Assert.Contains("id", result.OtherData);
        Assert.Contains("model", result.OtherData);
        Assert.Contains("system_fingerprint", result.OtherData);
        
        // Verify citations array
        var citations = result.OtherData["citations"];
        Assert.Equal(System.Text.Json.JsonValueKind.Array, citations.ValueKind);
        Assert.Equal(2, citations.GetArrayLength());
    }

    [Fact]
    public void Parse_xAIStreamingChunkWithCitations_ExtractsCitationsInOtherData()
    {
        // Arrange
        var json = """
        {
            "id": "5e773564-d6c4-da8f-26a1-729e0c17285c",
            "object": "chat.completion.chunk",
            "created": 1752222504,
            "model": "grok-3",
            "choices": [{
                "index": 0,
                "delta": {},
                "finish_reason": "stop"
            }],
            "system_fingerprint": "fp_9ad1a16f77",
            "citations": [
                "https://www.npr.org/sections/world/",
                "https://www.reuters.com/",
                "https://www.nytimes.com/section/world"
            ]
        }
        """;

        // Act
        var result = _parser.Parse(json, isRequest: false);

        // Assert
        // Delta with empty content should still create a message
        Assert.Single(result.Messages);
        
        // Check that citations are in OtherData
        Assert.Contains("citations", result.OtherData);
        var citations = result.OtherData["citations"];
        Assert.Equal(System.Text.Json.JsonValueKind.Array, citations.ValueKind);
        Assert.Equal(3, citations.GetArrayLength());
        
        // Check other fields
        Assert.Contains("id", result.OtherData);
        Assert.Contains("model", result.OtherData);
        Assert.Contains("system_fingerprint", result.OtherData);
    }

    [Fact]
    public void Parse_RequestWithToolCalls_AttachesToolCallsToMessage()
    {
        // Arrange
        var json = """
        {
            "model": "gpt-4",
            "messages": [
                {
                    "role": "assistant",
                    "content": null,
                    "tool_calls": [
                        {
                            "id": "call_123",
                            "type": "function",
                            "function": {
                                "name": "get_weather",
                                "arguments": "{\"location\": \"Tokyo\"}"
                            }
                        },
                        {
                            "id": "call_456",
                            "type": "function",
                            "function": {
                                "name": "get_time",
                                "arguments": "{\"timezone\": \"JST\"}"
                            }
                        }
                    ]
                }
            ]
        }
        """;

        // Act
        var result = _parser.Parse(json, isRequest: true);

        // Assert
        Assert.Single(result.Messages);
        var message = result.Messages[0];
        Assert.NotNull(message.ToolCalls);
        Assert.Equal(2, message.ToolCalls.Count);
        
        // First tool call
        Assert.Equal("call_123", message.ToolCalls[0].Id);
        Assert.Equal("get_weather", message.ToolCalls[0].Name);
        
        // Second tool call
        Assert.Equal("call_456", message.ToolCalls[1].Id);
        Assert.Equal("get_time", message.ToolCalls[1].Name);
        
        // Also check backward compatibility
        Assert.Equal(2, result.ToolCalls.Count);
    }

    [Fact]
    public void Parse_ResponseWithToolCalls_AttachesToolCallsToMessage()
    {
        // Arrange
        var json = """
        {
            "id": "chatcmpl-123",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": null,
                        "tool_calls": [
                            {
                                "id": "call_abc",
                                "type": "function",
                                "function": {
                                    "name": "calculate",
                                    "arguments": "{\"a\": 5, \"b\": 3}"
                                }
                            }
                        ]
                    },
                    "finish_reason": "tool_calls"
                }
            ]
        }
        """;

        // Act
        var result = _parser.Parse(json, isRequest: false);

        // Assert
        Assert.Single(result.Messages);
        var message = result.Messages[0];
        Assert.NotNull(message.ToolCalls);
        Assert.Single(message.ToolCalls);
        Assert.Equal("call_abc", message.ToolCalls[0].Id);
        Assert.Equal("calculate", message.ToolCalls[0].Name);
        
        // Also check backward compatibility
        Assert.Single(result.ToolCalls);
    }

    [Fact]
    public void Parse_StreamingResponseWithDeltaToolCalls_AttachesToolCallsToMessage()
    {
        // Arrange
        var json = """
        {
            "id": "chatcmpl-123",
            "object": "chat.completion.chunk",
            "created": 1677652288,
            "model": "gpt-4",
            "choices": [
                {
                    "index": 0,
                    "delta": {
                        "role": "assistant",
                        "tool_calls": [
                            {
                                "index": 0,
                                "id": "call_xyz",
                                "type": "function",
                                "function": {
                                    "name": "search",
                                    "arguments": "{\"query\": \"test\"}"
                                }
                            }
                        ]
                    },
                    "finish_reason": null
                }
            ]
        }
        """;

        // Act
        var result = _parser.Parse(json, isRequest: false);

        // Assert
        Assert.Single(result.Messages);
        var message = result.Messages[0];
        Assert.NotNull(message.ToolCalls);
        Assert.Single(message.ToolCalls);
        Assert.Equal("call_xyz", message.ToolCalls[0].Id);
        Assert.Equal("search", message.ToolCalls[0].Name);
        
        // Also check backward compatibility
        Assert.Single(result.ToolCalls);
    }

    [Fact]
    public void Parse_MixedMessagesWithAndWithoutToolCalls_HandlesCorrectly()
    {
        // Arrange
        var json = """
        {
            "model": "gpt-4",
            "messages": [
                {
                    "role": "user",
                    "content": "What's the weather?"
                },
                {
                    "role": "assistant",
                    "content": null,
                    "tool_calls": [
                        {
                            "id": "call_weather",
                            "type": "function",
                            "function": {
                                "name": "get_weather",
                                "arguments": "{\"location\": \"Tokyo\"}"
                            }
                        }
                    ]
                },
                {
                    "role": "tool",
                    "content": "Sunny, 25°C",
                    "tool_call_id": "call_weather"
                },
                {
                    "role": "assistant",
                    "content": "The weather in Tokyo is sunny with a temperature of 25°C."
                }
            ]
        }
        """;

        // Act
        var result = _parser.Parse(json, isRequest: true);

        // Assert
        Assert.Equal(4, result.Messages.Count);

#pragma warning disable CS8602 // Dereference of a possibly null reference.
        // First message (user) - no tool calls
        Assert.Equal("user", result.Messages[0].Role);
        Assert.Null(result.Messages[0].ToolCalls);
        
        // Second message (assistant) - has tool calls
        Assert.Equal("assistant", result.Messages[1].Role);
        Assert.NotNull(result.Messages[1].ToolCalls);
        Assert.Single(result.Messages[1].ToolCalls!);
        Assert.Equal("get_weather", result.Messages[1].ToolCalls[0].Name);
        
        // Third message (tool) - no tool calls
        Assert.Equal("tool", result.Messages[2].Role);
        Assert.Null(result.Messages[2].ToolCalls);
        
        // Fourth message (assistant) - no tool calls
        Assert.Equal("assistant", result.Messages[3].Role);
        Assert.Null(result.Messages[3].ToolCalls);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
    }

    [Fact]
    public void Parse_ToolMessageWithToolCallId_ExtractsOtherData()
    {
        // Arrange
        var json = """
        {
            "model": "gpt-4",
            "messages": [
                {
                    "role": "tool",
                    "content": "Sunny, 25°C",
                    "tool_call_id": "call_weather_123"
                }
            ]
        }
        """;

        // Act
        var result = _parser.Parse(json, isRequest: true);

        // Assert
        Assert.Single(result.Messages);
        var message = result.Messages[0];
        Assert.Equal("tool", message.Role);
        Assert.Equal("Sunny, 25°C", message.Content);
        Assert.NotNull(message.OtherData);
        Assert.True(message.OtherData.ContainsKey("tool_call_id"));
        Assert.Equal("call_weather_123", message.OtherData["tool_call_id"].GetString());
    }
}