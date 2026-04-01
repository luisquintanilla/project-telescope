using System.Text.Json;
using System.Text.Json.Serialization;

namespace Telescope.Extensions.AI.Events;

/// <summary>
/// Provides pre-configured JSON serialization for <see cref="EventKind"/> types,
/// producing output wire-compatible with the Rust Telescope collector.
/// </summary>
public static class EventSerializer
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
        return options;
    }

    public static string Serialize(EventKind eventKind) =>
        JsonSerializer.Serialize(eventKind, Options);

    public static EventKind? Deserialize(string json) =>
        JsonSerializer.Deserialize<EventKind>(json, Options);
}
