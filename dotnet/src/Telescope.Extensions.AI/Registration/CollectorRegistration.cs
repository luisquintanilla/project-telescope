using System.Text.Json.Serialization;

namespace Telescope.Extensions.AI.Registration;

public sealed class CollectorRegistration
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }
    public required AgentInfo Agent { get; init; }
}

public sealed class AgentInfo
{
    [JsonPropertyName("agent_id")]
    public required string AgentId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("agent_type")]
    public required string AgentType { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }
}
