using System.Text.Json;
using AIApiTracer.Services.MessageParsing;
using Xunit;

namespace AIApiTracer.Tests.Services.MessageParsing;

public class GeminiMessageParserTests
{
    private readonly GeminiMessageParser _parser = new();

    [Fact]
    public void CanParse_GeminiEndpoint_ReturnsTrue()
    {
        Assert.True(_parser.CanParse(EndpointType.Gemini));
    }

    [Fact]
    public void Parse_NativeGeminiRequest_ExtractsMessages()
    {
        // Arrange
        var requestBody = """
        {
          "contents": [
            {
              "role": "user",
              "parts": [{ "text": "Hello, how are you?" }]
            },
            {
              "role": "model",
              "parts": [{ "text": "I'm doing well, thank you!" }]
            },
            {
              "role": "user",
              "parts": [{ "text": "What is the weather today?" }]
            }
          ],
          "generationConfig": {
            "temperature": 0.7
          }
        }
        """;

        // Act
        var result = _parser.Parse(requestBody, true);

        // Assert
        Assert.Equal(3, result.Messages.Count);
        Assert.Equal("user", result.Messages[0].Role);
        Assert.Equal("Hello, how are you?", result.Messages[0].Content);
        Assert.Equal("assistant", result.Messages[1].Role);
        Assert.Equal("I'm doing well, thank you!", result.Messages[1].Content);
        Assert.Equal("user", result.Messages[2].Role);
        Assert.Equal("What is the weather today?", result.Messages[2].Content);
        Assert.True(result.OtherData.ContainsKey("generationConfig"));
    }

    [Fact]
    public void Parse_NativeGeminiResponse_ExtractsMessages()
    {
        // Arrange
        var responseBody = """
        {
          "candidates": [
            {
              "content": {
                "role": "model",
                "parts": [{ "text": "The weather is sunny." }]
              },
              "finishReason": "STOP"
            }
          ],
          "usageMetadata": {
            "totalTokenCount": 10
          }
        }
        """;

        // Act
        var result = _parser.Parse(responseBody, false);

        // Assert
        Assert.Single(result.Messages);
        Assert.Equal("assistant", result.Messages[0].Role);
        Assert.Equal("The weather is sunny.", result.Messages[0].Content);
        Assert.Equal("STOP", result.Messages[0].OtherData!["finishReason"].GetString());
        Assert.True(result.OtherData.ContainsKey("usageMetadata"));
    }

    [Fact]
    public void Parse_GeminiRequestWithSystemInstruction_ExtractsSystemMessage()
    {
        // Arrange
        var requestBody = """
        {
          "systemInstruction": {
            "parts": [{ "text": "You are a helpful assistant." }]
          },
          "contents": [
            {
              "role": "user",
              "parts": [{ "text": "Hi" }]
            }
          ]
        }
        """;

        // Act
        var result = _parser.Parse(requestBody, true);

        // Assert
        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("system", result.Messages[0].Role);
        Assert.Equal("You are a helpful assistant.", result.Messages[0].Content);
        Assert.Equal("user", result.Messages[1].Role);
        Assert.Equal("Hi", result.Messages[1].Content);
    }

    [Fact]
    public void Parse_GeminiToolCall_ExtractsToolCalls()
    {
        // Arrange
        var responseBody = """
        {
          "candidates": [
            {
              "content": {
                "role": "model",
                "parts": [
                  {
                    "functionCall": {
                      "name": "get_weather",
                      "args": { "location": "London" }
                    }
                  }
                ]
              }
            }
          ]
        }
        """;

        // Act
        var result = _parser.Parse(responseBody, false);

        // Assert
        Assert.Single(result.Messages);
        var message = result.Messages[0];
        Assert.NotNull(message.ToolCalls);
        Assert.Single(message.ToolCalls);
        Assert.Equal("get_weather", message.ToolCalls[0].Name);
        Assert.Equal("London", message.ToolCalls[0].Arguments!.Value.GetProperty("location").GetString());
    }

    [Fact]
    public void Parse_NativeGeminiStreamingResponse_MergesMessages()
    {
        // Arrange
        var responseBody = """
        [
          {
            "candidates": [
              {
                "content": {
                  "role": "model",
                  "parts": [{ "text": "Part 1 " }]
                }
              }
            ]
          },
          {
            "candidates": [
              {
                "content": {
                  "role": "model",
                  "parts": [{ "text": "Part 2" }]
                }
              }
            ]
          }
        ]
        """;

        // Act
        var result = _parser.Parse(responseBody, false);

        // Assert
        Assert.Single(result.Messages);
        Assert.Equal("assistant", result.Messages[0].Role);
        Assert.Equal("Part 1 Part 2", result.Messages[0].Content);
    }
}
