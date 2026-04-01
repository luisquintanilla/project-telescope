# .NET Microsoft.Extensions.AI Example

A minimal example showing how to integrate .NET AI applications with Project Telescope using Microsoft.Extensions.AI middleware. Intercepts `IChatClient` calls and emits Telescope observability events over the named pipe protocol.

## What it demonstrates

- **`DelegatingChatClient` middleware** that wraps any M.E.AI `IChatClient`
- **Automatic event emission:** session lifecycle, turns, tool calls, token usage, errors
- **GitHub Models** — no Azure subscription required, just a GitHub PAT
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
- A [GitHub personal access token](https://github.com/settings/tokens) (PAT)

## Quick start

```bash
# 1. Start Telescope
tele service start

# 2. Store your GitHub token (one-time setup)
cd examples/dotnet-meai
dotnet user-secrets --project ChatWithTelescope set "GitHub:Token" "ghp_your_token_here"

# 3. Run the example
dotnet run --project ChatWithTelescope

# 4. View events in the Dashboard
telescope-dashboard
# Or query via CLI:
tele sessions list
tele turns list <session-id>
```

You can optionally set the model: `dotnet user-secrets --project ChatWithTelescope set "GitHub:Model" "openai/gpt-4o"`

Environment variables (`GITHUB_TOKEN`, `GITHUB_MODEL`) also work as a fallback.

## Pipeline architecture

The M.E.AI middleware ordering matters — `TelescopeChatClient` sits outermost so it sees all messages including function calls and results from `FunctionInvokingChatClient`'s internal loop:

```
IChatClient pipeline:
  TelescopeChatClient (outermost — sees everything)
    → FunctionInvokingChatClient (handles tool loops)
      → OpenAIChatClient (GitHub Models)
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
├── ChatWithTelescope/            # Sample app (GitHub Models + function calling)
├── DotnetMeaiExample.slnx       # Solution file
└── nuget.config
```

