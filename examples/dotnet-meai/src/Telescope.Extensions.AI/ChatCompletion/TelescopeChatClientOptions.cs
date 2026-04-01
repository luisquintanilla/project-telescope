// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Telescope.Extensions.AI.Transport;

namespace Telescope.Extensions.AI.ChatCompletion;

/// <summary>
/// Configuration options for the <see cref="TelescopeChatClient"/> middleware.
/// </summary>
public sealed class TelescopeChatClientOptions
{
    /// <summary>Stable agent identifier (used for deterministic UUID generation).</summary>
    public required string AgentId { get; set; }

    /// <summary>Human-readable agent display name.</summary>
    public required string AgentName { get; set; }

    /// <summary>Agent type classification (default: "ai-assistant").</summary>
    public string AgentType { get; set; } = "ai-assistant";

    /// <summary>Agent version string.</summary>
    public string? AgentVersion { get; set; }

    /// <summary>Collector name for registration.</summary>
    public string CollectorName { get; set; } = "telescope-dotnet-meai";

    /// <summary>Collector version.</summary>
    public string CollectorVersion { get; set; } = "0.1.0";

    /// <summary>Transport options (pipe name, retry config, etc.).</summary>
    public TelescopeTransportOptions Transport { get; set; } = new();

    /// <summary>Whether to capture message content (may contain sensitive data).</summary>
    public bool EnableSensitiveData { get; set; } = false;

    /// <summary>
    /// Emit OpenTelemetry spans with gen_ai.* semantic conventions.
    /// Default: true. Zero-cost if no OTEL exporter is configured —
    /// ActivitySource.StartActivity() returns null when nobody is listening.
    /// </summary>
    public bool EmitOpenTelemetrySpans { get; set; } = true;
}
