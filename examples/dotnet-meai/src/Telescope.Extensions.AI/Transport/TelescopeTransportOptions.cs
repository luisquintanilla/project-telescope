namespace Telescope.Extensions.AI.Transport;

public sealed class TelescopeTransportOptions
{
    /// <summary>Named pipe name. Default: "telescope-collector".</summary>
    public string PipeName { get; set; } = "telescope-collector";

    /// <summary>Connection timeout.</summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum retry attempts for initial connection.</summary>
    public int MaxConnectRetries { get; set; } = 10;

    /// <summary>Maximum events per submit batch.</summary>
    public int MaxBatchSize { get; set; } = 500;

    /// <summary>Heartbeat interval when no events submitted.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);
}
