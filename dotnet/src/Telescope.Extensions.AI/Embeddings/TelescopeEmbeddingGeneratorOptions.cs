using Telescope.Extensions.AI.Transport;

namespace Telescope.Extensions.AI.Embeddings;

public sealed class TelescopeEmbeddingGeneratorOptions
{
    /// <summary>Stable agent identifier.</summary>
    public required string AgentId { get; set; }

    /// <summary>Human-readable agent display name.</summary>
    public required string AgentName { get; set; }

    /// <summary>Agent type classification (default: "embedding-service").</summary>
    public string AgentType { get; set; } = "embedding-service";

    /// <summary>Agent version string.</summary>
    public string? AgentVersion { get; set; }

    /// <summary>Collector name for registration.</summary>
    public string CollectorName { get; set; } = "telescope-dotnet-meai-embeddings";

    /// <summary>Collector version.</summary>
    public string CollectorVersion { get; set; } = "0.1.0";

    /// <summary>Transport options.</summary>
    public TelescopeTransportOptions Transport { get; set; } = new();
}
