# Telescope.Extensions.AI — .NET Bindings for Project Telescope

[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) middleware that gives you **local-first observability** for your .NET AI applications. Drop it into your `IChatClient` or `IEmbeddingGenerator` pipeline and every LLM call, tool invocation, and token spend becomes visible in [Project Telescope](../README.md) — without sending a single byte off your machine.

---

## Quick Start

```csharp
using Microsoft.Extensions.AI;
using Telescope.Extensions.AI.ChatCompletion;

IChatClient client = new OpenAIClient(apiKey)
    .GetChatClient("gpt-4o-mini").AsIChatClient()
    .AsBuilder()
    .UseTelescope(o =>
    {
        o.AgentId = "my-dotnet-app";
        o.AgentName = "My Application";
    })
    .UseFunctionInvocation()
    .Build();

// All chat calls are now observable in Telescope
var response = await client.GetResponseAsync("Hello!");
```

That's it. Open the Telescope dashboard or run `tele sessions list` to see your agent activity.

---

## What Gets Captured

| M.E.AI Operation | Telescope Event(s) |
|---|---|
| `GetResponseAsync` / `GetStreamingResponseAsync` | `TurnStarted`, `TurnCompleted`, `UserMessage` |
| `FunctionCallContent` in response | `ToolCallStarted` |
| `FunctionResultContent` in messages | `ToolCallCompleted` |
| `ChatResponse.Usage` | `TokenUsageReported` |
| Exceptions | `ErrorOccurred` |
| `GenerateAsync` (embeddings) | `ModelUsed`, `TokenUsageReported`, `Custom` |

---

## Pipeline Ordering

`UseTelescope` should be the **outermost** middleware so it observes everything that happens inside the pipeline:

```csharp
IChatClient client = innerClient
    .AsBuilder()
    .UseTelescope(...)          // outermost — sees everything
    .UseOpenTelemetry()         // optional OTel traces
    .UseFunctionInvocation()    // handles tool calls
    .Build();
```

Because `DelegatingChatClient` wraps inward, the first call in the builder chain runs first on the way in and last on the way out — giving Telescope full visibility over the entire request lifecycle, including tool calls and retries.

---

## Embedding Generator

```csharp
using Microsoft.Extensions.AI;
using Telescope.Extensions.AI.Embeddings;

IEmbeddingGenerator<string, Embedding<float>> generator = new OpenAIClient(apiKey)
    .GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator()
    .AsBuilder()
    .UseTelescope(o =>
    {
        o.AgentId = "my-embeddings";
        o.AgentName = "Embedding Service";
    })
    .Build();
```

The embedding middleware emits `ModelUsed`, `TokenUsageReported`, and a `Custom` event (`embedding_generated`) with input count, dimensions, and duration.

---

## Configuration Options

### `TelescopeChatClientOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `AgentId` | `string` | *(required)* | Stable identifier used for deterministic UUID generation |
| `AgentName` | `string` | *(required)* | Human-readable display name shown in the dashboard |
| `AgentType` | `string` | `"ai-assistant"` | Classification type for the agent |
| `AgentVersion` | `string?` | `null` | Version string of your application |
| `CollectorName` | `string` | `"telescope-dotnet-meai"` | Name used when registering with the Telescope service |
| `CollectorVersion` | `string` | `"0.1.0"` | Collector version reported during registration |
| `Transport` | `TelescopeTransportOptions` | *(see below)* | Transport-level configuration |
| `EnableSensitiveData` | `bool` | `false` | When `true`, captures message content in events |

### `TelescopeEmbeddingGeneratorOptions`

Same shape as chat options, with defaults tuned for embeddings (`AgentType` = `"embedding-service"`, `CollectorName` = `"telescope-dotnet-meai-embeddings"`).

### `TelescopeTransportOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `PipeName` | `string` | `"telescope-collector"` | Named pipe name to connect to |
| `ConnectTimeout` | `TimeSpan` | 5 seconds | Timeout for each connection attempt |
| `MaxConnectRetries` | `int` | `10` | Maximum retry attempts with exponential backoff |
| `MaxBatchSize` | `int` | `500` | Maximum events per batch submission |
| `HeartbeatInterval` | `TimeSpan` | 30 seconds | Interval between heartbeat pings |

---

## Architecture

- **Pure C#** — no native dependencies; just `Microsoft.Extensions.AI`
- **Named pipe IPC** — connects to the Telescope service via named pipes (`\\.\pipe\telescope-collector` on Windows)
- **Length-prefixed JSON-RPC** — 4-byte little-endian length prefix followed by UTF-8 JSON payloads, wire-compatible with the Rust SDK collectors
- **Graceful degradation** — if the Telescope service is unavailable, the middleware silently skips event submission and never breaks your AI pipeline
- **Thread-safe** — all transport access is guarded by a semaphore; safe for concurrent use across async calls

---

## How It Works

1. **`TelescopeChatClient`** extends `DelegatingChatClient` and wraps your inner `IChatClient`
2. On each call, it intercepts `GetResponseAsync` / `GetStreamingResponseAsync`
3. Before the inner call, it emits `UserMessage` and `TurnStarted` events
4. After the inner call, it inspects `ChatResponse.Messages` for `FunctionCallContent` and `FunctionResultContent` to emit `ToolCallStarted` / `ToolCallCompleted`
5. Token usage from `ChatResponse.Usage` is reported as `TokenUsageReported`
6. All events are submitted to the Telescope service over named pipe IPC
7. If the service is unreachable, events are silently dropped — your AI pipeline is never disrupted

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Telescope service installed and running — see [install instructions](../README.md#install)
- `Microsoft.Extensions.AI` 10.4.1 (pulled automatically as a package dependency)

---

## Building from Source

```bash
cd dotnet
dotnet build
dotnet test
```

---

## License

[MIT](../LICENSE)
