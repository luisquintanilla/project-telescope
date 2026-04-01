# .NET Microsoft.Extensions.AI Example

A minimal example showing how to integrate .NET AI applications with Project Telescope using Microsoft.Extensions.AI middleware. Intercepts `IChatClient` calls and emits Telescope observability events over the named pipe protocol.

## What it demonstrates

- **`DelegatingChatClient` middleware** that wraps any M.E.AI `IChatClient`
- **Automatic event emission:** session lifecycle, turns, tool calls, token usage, errors
- **Azure OpenAI with managed identity** (`DefaultAzureCredential`)
- **Function calling with `UseFunctionInvocation()`** — Telescope captures all tool calls

## Event flow

Events emitted during a typical chat with function calling:

```
SessionStarted → UserMessage → TurnStarted
  → ToolCallStarted (GetCurrentWeather)
  → ToolCallCompleted
  → ToolCallStarted (GetCurrentTime)
  → ToolCallCompleted
  → TokenUsageReported
→ TurnCompleted
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Project Telescope installed ([download MSI](https://github.com/microsoft/project-telescope/releases))
- Azure OpenAI resource with a chat model deployment
- Azure CLI logged in (`az login`) for `DefaultAzureCredential`

## Quick start

```bash
# 1. Start Telescope
tele service start

# 2. Set your Azure OpenAI endpoint
set AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/

# 3. Run the example
cd examples/dotnet-meai
dotnet run --project ChatWithTelescope

# 4. View events in the Dashboard
telescope-dashboard
# Or query via CLI:
tele sessions list
tele turns list <session-id>
```

## Pipeline architecture

The M.E.AI middleware ordering matters — `TelescopeChatClient` sits outermost so it sees all messages including function calls and results from `FunctionInvokingChatClient`'s internal loop:

```
IChatClient pipeline:
  TelescopeChatClient (outermost — sees everything)
    → FunctionInvokingChatClient (handles tool loops)
      → AzureOpenAIChatClient (actual LLM calls)
```

## Configuration

`TelescopeChatClientOptions`:

| Option | Default | Description |
|--------|---------|-------------|
| `AgentId` | *(required)* | Stable agent identifier |
| `AgentName` | *(required)* | Human-readable agent name |
| `EnableSensitiveData` | `false` | Capture message content |
| `Transport.PipeName` | `"telescope-collector"` | Named pipe name |

## Graceful degradation

If Telescope isn't running, the middleware silently skips telemetry — the AI pipeline works normally. No exceptions, no broken calls.

## Project structure

```
dotnet-meai/
├── src/Telescope.Extensions.AI/  # Minimal middleware library
├── ChatWithTelescope/            # Sample app (Azure OpenAI + function calling)
├── DotnetMeaiExample.slnx       # Solution file
└── nuget.config
```

## Full SDK

For the full SDK with all 56 event types, `IEmbeddingGenerator` support, and test suite, see the [`feature/dotnet-meai-bindings`](https://github.com/microsoft/project-telescope/tree/feature/dotnet-meai-bindings) branch.
