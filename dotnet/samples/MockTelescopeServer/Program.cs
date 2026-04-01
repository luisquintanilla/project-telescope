using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

const string PipeName = "telescope-collector";
const int MaxFrameSize = 16 * 1024 * 1024; // 16 MiB

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    WriteIndented = false,
};

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine("🔭 Mock Telescope Server");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($@"Listening on: \\.\pipe\{PipeName}");

var ct = cts.Token;

try
{
    while (!ct.IsCancellationRequested)
    {
        using var server = new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        Console.WriteLine("Waiting for collector to connect...");
        await server.WaitForConnectionAsync(ct);
        Console.WriteLine("Collector connected!");

        try
        {
            while (server.IsConnected)
            {
                var request = await ReadFrameAsync(server, ct);
                if (request is null)
                    break;

                var (response, shouldDisconnect) = HandleRequest(request);
                await WriteFrameAsync(server, response, ct);

                if (shouldDisconnect)
                    break;
            }
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("Collector disconnected.");
        }
        catch (IOException)
        {
            Console.WriteLine("Collector disconnected (broken pipe).");
        }
    }
}
catch (OperationCanceledException) when (ct.IsCancellationRequested)
{
    // Normal shutdown
}

Console.WriteLine("\nServer shutting down.");
return;

// ─── Frame I/O ───────────────────────────────────────────────────────

async Task<JsonNode?> ReadFrameAsync(Stream stream, CancellationToken cancellation)
{
    var lengthBuf = new byte[4];
    if (!await ReadExactAsync(stream, lengthBuf, cancellation))
        return null;

    var length = BinaryPrimitives.ReadUInt32LittleEndian(lengthBuf);
    if (length == 0 || length > MaxFrameSize)
        throw new InvalidDataException($"Invalid frame length: {length}");

    var payload = new byte[length];
    if (!await ReadExactAsync(stream, payload, cancellation))
        throw new EndOfStreamException("Connection closed mid-frame.");

    return JsonNode.Parse(payload);
}

async Task WriteFrameAsync(Stream stream, JsonNode response, CancellationToken cancellation)
{
    var payload = Encoding.UTF8.GetBytes(response.ToJsonString(jsonOptions));

    var lengthBuf = new byte[4];
    BinaryPrimitives.WriteUInt32LittleEndian(lengthBuf, (uint)payload.Length);

    await stream.WriteAsync(lengthBuf, cancellation);
    await stream.WriteAsync(payload, cancellation);
    await stream.FlushAsync(cancellation);
}

async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellation)
{
    var offset = 0;
    while (offset < buffer.Length)
    {
        var read = await stream.ReadAsync(
            buffer.AsMemory(offset, buffer.Length - offset), cancellation);
        if (read == 0)
            return false;
        offset += read;
    }
    return true;
}

// ─── Request Handling ────────────────────────────────────────────────

(JsonNode Response, bool ShouldDisconnect) HandleRequest(JsonNode request)
{
    var method = request["method"]?.GetValue<string>() ?? "unknown";
    var parameters = request["params"];

    return method switch
    {
        "collector.register" => (HandleRegister(parameters), false),
        "collector.submit" => (HandleSubmit(parameters), false),
        "collector.heartbeat" => (HandleHeartbeat(), false),
        "collector.deregister" => (HandleDeregister(), true),
        _ => (HandleUnknownMethod(method), false),
    };
}

JsonNode HandleRegister(JsonNode? parameters)
{
    var collectorId = Guid.NewGuid().ToString();
    var name = parameters?["name"]?.GetValue<string>() ?? "unknown";
    var agent = parameters?["agent"];
    var agentName = agent?["name"]?.GetValue<string>() ?? "unknown";
    var agentType = agent?["agent_type"]?.GetValue<string>() ?? "unknown";
    var agentId = agent?["agent_id"]?.GetValue<string>() ?? "unknown";
    var pid = parameters?["pid"]?.GetValue<int>() ?? 0;

    WriteColored(ConsoleColor.Green, $"""

    ✅ Collector registered!
       Name:      {name}
       Agent:     {agentName} ({agentType})
       Agent ID:  {agentId}
       PID:       {pid}
       Collector ID assigned: {collectorId}
    """);

    return new JsonObject
    {
        ["result"] = new JsonObject
        {
            ["status"] = "ok",
            ["collector_id"] = collectorId,
            ["max_batch_size"] = 500,
        },
    };
}

JsonNode HandleSubmit(JsonNode? parameters)
{
    var events = parameters?["events"]?.AsArray();
    var count = events?.Count ?? 0;

    if (events is not null)
    {
        foreach (var evt in events)
        {
            if (evt is null) continue;
            PrettyPrintEvent(evt);
        }
    }

    return new JsonObject
    {
        ["result"] = new JsonObject
        {
            ["accepted"] = count,
            ["delay_hint_ms"] = 0,
        },
    };
}

JsonNode HandleHeartbeat()
{
    return new JsonObject
    {
        ["result"] = new JsonObject
        {
            ["status"] = "ok",
        },
    };
}

JsonNode HandleDeregister()
{
    WriteColored(ConsoleColor.Yellow, "⚠️  Collector deregistered. Waiting for next connection...\n");

    return new JsonObject
    {
        ["result"] = new JsonObject
        {
            ["status"] = "ok",
        },
    };
}

JsonNode HandleUnknownMethod(string method)
{
    WriteColored(ConsoleColor.Red, $"❌ Unknown method: {method}\n");

    return new JsonObject
    {
        ["error"] = new JsonObject
        {
            ["code"] = -32601,
            ["message"] = $"Method not found: {method}",
        },
    };
}

// ─── Pretty-Print Events ────────────────────────────────────────────

void PrettyPrintEvent(JsonNode evt)
{
    var type = evt["type"]?.GetValue<string>() ?? "unknown";
    var color = GetEventColor(type);
    var separator = new string('─', 50);

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine(separator);

    Console.ForegroundColor = color;
    Console.WriteLine($"📡 Event: {type}");

    Console.ForegroundColor = ConsoleColor.Gray;
    foreach (var prop in evt.AsObject())
    {
        if (prop.Key == "type") continue;

        var value = prop.Value is JsonObject or JsonArray
            ? prop.Value.ToJsonString(jsonOptions)
            : prop.Value?.ToString() ?? "null";

        Console.WriteLine($"   {prop.Key,-16}{value}");
    }

    Console.ResetColor();
}

ConsoleColor GetEventColor(string eventType) => eventType switch
{
    _ when eventType.EndsWith("_started") => ConsoleColor.Green,
    _ when eventType.EndsWith("_completed") => ConsoleColor.Cyan,
    "user_message" => ConsoleColor.Yellow,
    "token_usage_reported" => ConsoleColor.Magenta,
    "error_occurred" => ConsoleColor.Red,
    _ => ConsoleColor.White,
};

void WriteColored(ConsoleColor color, string text)
{
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ResetColor();
}
