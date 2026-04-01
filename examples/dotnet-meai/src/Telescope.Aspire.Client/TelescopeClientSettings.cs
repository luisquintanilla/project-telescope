namespace Telescope.Aspire.Client;

/// <summary>
/// Configuration settings for the Telescope Aspire client integration.
/// </summary>
public sealed class TelescopeClientSettings
{
    /// <summary>
    /// Named pipe to connect to. Populated from the Aspire connection string
    /// or configuration section.
    /// </summary>
    public string PipeName { get; set; } = "telescope-collector";

    /// <summary>Agent identifier for registration with Telescope service.</summary>
    public string AgentId { get; set; } = "aspire-app";

    /// <summary>Human-readable agent name.</summary>
    public string AgentName { get; set; } = "Aspire Application";

    /// <summary>Agent version string.</summary>
    public string? AgentVersion { get; set; }

    /// <summary>Whether to capture message content in events.</summary>
    public bool EnableSensitiveData { get; set; }

    /// <summary>Whether to emit OpenTelemetry spans alongside Telescope events.</summary>
    public bool EmitOpenTelemetrySpans { get; set; } = true;

    /// <summary>Disable health checks.</summary>
    public bool DisableHealthChecks { get; set; }

    /// <summary>Health check timeout in milliseconds.</summary>
    public int? HealthCheckTimeout { get; set; }

    /// <summary>Max retries when connecting to the named pipe.</summary>
    public int MaxConnectRetries { get; set; } = 10;

    /// <summary>Timeout per connection attempt.</summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Parse connection string. For Telescope, the connection string
    /// is simply the pipe name.
    /// </summary>
    internal void ParseConnectionString(string connectionString)
    {
        PipeName = connectionString;
    }
}
