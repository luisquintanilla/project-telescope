using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Telescope.Extensions.AI.Transport;

/// <summary>
/// An IPC request to the Telescope service.
/// JSON-RPC-inspired: { "method": "...", "params": {...} }
/// </summary>
public sealed class IpcRequest
{
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("params")]
    public JsonNode? Params { get; init; }
}
