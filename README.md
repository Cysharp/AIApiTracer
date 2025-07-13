# AIApiTracer

A reverse proxy for local development environments that intercepts requests to AI services like OpenAI, Anthropic, Azure OpenAI, and xAI, allowing you to trace request and response content.

Simply put, it's like [Cloudflare AI Gateway Logs](https://developers.cloudflare.com/ai-gateway/observability/logging/) specialized for local development environments. Note that it's not intended for auditing, monitoring, or team usage.

![](docs/img/screen-01.png?)

## Features

- Human-readable display of requests and responses
- Support for multiple AI services
    - OpenAI
    - Anthropic
    - Microsoft Azure OpenAI
    - xAI
    - OpenAI Compatible APIs
- In-memory: No data persistence, retains up to 1000 records

## Quick Start

### 1. Start AIApiTracer

Start AIApiTracer using the Docker image (ghcr.io/Cysharp/AIApiTracer).

```bash
docker run -p 8080:8080 ghcr.io/cysharp/aiapitracer:latest
```

Once started, you can view request traces by accessing `http://localhost:8080` in your web browser.

### 2. Change Various Endpoints to AIApiTracer

#### OpenAI

Specify `http://localhost:8080/endpoint/openai/v1` as the endpoint instead of `https://api.openai.com/v1`.

```bash
curl http://localhost:8080/endpoint/openai/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $API_KEY" \
  -d '{
  "messages": [
    {
      "role": "user",
      "content": "Hello."
    }
  ],
  "model": "gpt-4.1-nano"
}'
```
```csharp
var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
{
    Endpoint = new Uri($"http://localhost:8080/endpoint/openai/v1"),
});
```

#### Anthropic
Specify `http://localhost:8080/endpoint/anthropic` as the endpoint instead of `https://api.anthropic.com/`.

#### Claude Code
```bash
export ANTHROPIC_BASE_URL=http://localhost:8080/endpoint/anthropic
```

#### Azure OpenAI
Specify `http://localhost:8080/endpoint/aoai/<resource-name>` as the endpoint instead of `https://<resource-name>.openai.azure.com/`.

```csharp
var azureClient = new AzureOpenAIClient(
    new Uri("http://localhost:8080/endpoint/aoai/my-resource"),
    new AzureKeyCredential(credential)
);
```

#### xAI
Specify `http://localhost:8080/endpoint/x/v1` as the endpoint instead of `https://api.x.ai/v1`.

```bash
curl http://localhost:8080/endpoint/x/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $API_KEY" \
  -d '{
  "messages": [
    {
      "role": "user",
      "content": "Hello."
    }
  ],
  "model": "grok-3-latest"
}'
```
```csharp
var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
{
    Endpoint = new Uri($"http://localhost:8080/endpoint/x/v1"),
});
```

#### OpenAI Compatible
Specify `http://localhost:8080/endpoint/openai-compat/<openai-compatible-endpoint>` as the endpoint.

⚠️ **Security Notice**: The OpenAI compatible endpoint is disabled by default for security reasons. You must explicitly enable it by setting the environment variable `AIApiTracer__EnableOpenAICompatForwarding` to `true`. When enabled, you should absolutely avoid making it accessible from external networks as it would become an open proxy.

Specify the address of the OpenAI compatible API endpoint after `openai-compat/`, including the scheme. For example, use `http://localhost:8080/endpoint/openai-compat/http://localhost:5273/v1`.

```bash
curl http://localhost:8080/endpoint/openai-compat/http://localhost:5273/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $API_KEY" \
  -d '{
  "messages": [
    {
      "role": "user",
      "content": "Hello."
    }
  ],
  "model": "Phi-4-mini-instruct-cuda-gpu"
}'
```
```csharp
var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
{
    Endpoint = new Uri($"http://localhost:8080/endpoint/openai-compat/http://localhost:5273/v1"), // `http://localhost:5273/v1` is Foundry Local
});
```

## TODO
- [ ] Support for more AI services (Gemini)
- [ ] Show about rate limit information

## License

MIT License