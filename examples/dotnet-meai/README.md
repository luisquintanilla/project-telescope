# .NET Microsoft.Extensions.AI Example

A minimal example showing how to integrate .NET AI applications with Project Telescope using Microsoft.Extensions.AI middleware. Intercepts `IChatClient` calls and emits Telescope observability events over the named pipe protocol — with optional Aspire orchestration and OpenTelemetry dual-export.

## What it demonstrates

- **`DelegatingChatClient` middleware** that wraps any M.E.AI `IChatClient`
- **Automatic event emission:** session lifecycle, turns, tool calls, token usage, errors
- **GitHub Models** — no Azure subscription required, just a GitHub PAT
- **Function calling with `UseFunctionInvocation()`** — Telescope captures all tool calls
- **OpenTelemetry `gen_ai.*` spans** — zero-cost OTEL emission alongside Telescope events
- **Aspire AppHost orchestration** — run with `dotnet run --project AppHost` for dual-dashboard observability
- **Aspire hosting/client packages** — model Telescope as an Aspire resource with health checks and DI

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

### Option A: Run standalone

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

### Option B: Run with Aspire

```bash
# 1. Start Telescope service
tele service start

# 2. Store your GitHub token as an Aspire parameter (one-time setup)
cd examples/dotnet-meai
dotnet user-secrets --project AppHost set "Parameters:github-token" "ghp_your_token_here"
# Or skip this — the Aspire Dashboard will prompt you to enter it

# 3. Run via Aspire AppHost
dotnet run --project AppHost

# 4. Open the Aspire Dashboard (URL shown in AppHost output)
#    → View OTEL traces with gen_ai.* spans
#    → See token usage, model info, tool call hierarchy

# 5. Also open Telescope Dashboard for rich AI event stream
telescope-dashboard
```

You can optionally set the model: `dotnet user-secrets --project ChatWithTelescope set "GitHub:Model" "openai/gpt-4o"`

Environment variables (`GITHUB_TOKEN`, `GITHUB_MODEL`) also work as a fallback.

## Pipeline architecture

The M.E.AI middleware ordering matters — `TelescopeChatClient` sits outermost so it sees all messages including function calls and results from `FunctionInvokingChatClient`'s internal loop:

```
IChatClient pipeline:
  TelescopeChatClient (outermost — sees everything)
    ├── Emits Telescope events → Named pipe → Telescope Dashboard
    └── Emits OTEL gen_ai.* spans → OTLP exporter → Aspire Dashboard
    → FunctionInvokingChatClient (handles tool loops)
      → OpenAIChatClient (GitHub Models)
```

Both export paths have independent graceful degradation:
- **Telescope pipe unavailable** → events silently skipped
- **No OTEL exporter configured** → `ActivitySource.StartActivity()` returns null (zero cost)

## Dual-dashboard observability

With Aspire, the same application appears in **both** dashboards:

| Dashboard | What you see | Protocol |
|-----------|-------------|----------|
| **Aspire Dashboard** | `gen_ai.chat` traces, token usage, tool call hierarchy, model info, alongside HTTP/SQL/gRPC traces from the rest of your app | OTLP (gRPC/HTTP) |
| **Telescope Dashboard** | Rich 56-event AI agent observability — sessions, turns, tool calls, planning, sub-agents, human-in-the-loop | Named pipe IPC |

They're complementary: Aspire is "distributed app health." Telescope is "AI agent behavior."

## OpenTelemetry integration

`TelescopeChatClient` emits `System.Diagnostics.Activity` spans using an `ActivitySource("Telescope.Extensions.AI")`. When an OTEL exporter is configured (e.g., via Aspire ServiceDefaults), spans flow automatically:

| Telescope Event | OTEL Activity | Key Attributes |
|-----------------|---------------|----------------|
| `TurnStarted/Completed` | `gen_ai.chat` (Client) | `gen_ai.system`, `gen_ai.operation.name`, `gen_ai.request.model`, `gen_ai.response.model` |
| `ToolCallStarted/Completed` | `gen_ai.tool.{name}` (Internal) | `gen_ai.tool.name` |
| `TokenUsageReported` | Tags on parent activity | `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`, `gen_ai.usage.total_tokens` |
| `ErrorOccurred` | `Activity.SetStatus(Error)` | `exception.type`, `exception.message` |

Controlled by `TelescopeChatClientOptions.EmitOpenTelemetrySpans` (default: `true`).

## Aspire hosting integration

The `Telescope.Aspire.Hosting` package models Telescope as an Aspire resource:

```csharp
// AppHost/Program.cs
var telescope = builder.AddTelescope("telescope");         // starts tele service
builder.AddTelescopeDashboard();                            // starts dashboard UI
builder.AddProject<Projects.MyApp>("my-app")
    .WithReference(telescope);                              // injects pipe name
```

The `Telescope.Aspire.Client` package provides DI registration:

```csharp
// MyApp/Program.cs
builder.AddTelescopeClient("telescope");                    // reads pipe name from connection string

builder.Services.AddChatClient(services =>
    new ChatClientBuilder(services)
        .UseTelescope(services)                             // Telescope middleware from DI
        .UseFunctionInvocation()
        .Use(innerClient));
```

Key design: health checks return `Degraded` (not `Unhealthy`) — Telescope is optional.

## Configuration

`TelescopeChatClientOptions`:

| Option | Default | Description |
|--------|---------|-------------|
| `AgentId` | *(required)* | Stable agent identifier |
| `AgentName` | *(required)* | Human-readable agent name |
| `EnableSensitiveData` | `false` | Capture message content |
| `EmitOpenTelemetrySpans` | `true` | Emit OTEL `gen_ai.*` spans (zero-cost if no exporter) |
| `Transport.PipeName` | `"telescope-collector"` | Named pipe name |

## Graceful degradation

If Telescope isn't running, the middleware silently skips telemetry — the AI pipeline works normally. No exceptions, no broken calls. Similarly, OTEL spans are only allocated when an exporter is listening.

## Project structure

```
dotnet-meai/
├── AppHost/                           # Aspire AppHost (orchestration)
├── ServiceDefaults/                   # OTEL + health checks + service discovery
├── ChatWithTelescope/                 # Sample app (GitHub Models + function calling)
├── src/
│   ├── Telescope.Extensions.AI/       # Core middleware library
│   ├── Telescope.Aspire.Hosting/      # Aspire hosting integration
│   └── Telescope.Aspire.Client/       # Aspire client integration (DI + health checks)
├── DotnetMeaiExample.slnx            # Solution file
└── nuget.config
```

