using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Telescope.Extensions.AI.Transport;

/// <summary>
/// An IPC response from the Telescope service.
/// </summary>
public sealed class IpcResponse
{
    [JsonPropertyName("result")]
    public JsonNode? Result { get; set; }

    [JsonPropertyName("error")]
    public IpcError? Error { get; set; }

    public bool IsError => Error is not null;
}

public sealed class IpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
