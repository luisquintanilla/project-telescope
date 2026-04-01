using System.Text.Json;
using System.Text.Json.Nodes;
using Telescope.Extensions.AI.Transport;
using Xunit;

namespace Telescope.Extensions.AI.Tests.Transport;

public class IpcProtocolTests
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // ── IpcRequest tests ────────────────────────────────────────────────────

    [Fact]
    public void IpcRequest_Serializes_WithMethodAndParams()
    {
        var request = new IpcRequest
        {
            Method = "collector.register",
            Params = new JsonObject { ["name"] = "test-collector" },
        };

        var json = JsonSerializer.Serialize(request, s_options);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("collector.register", root.GetProperty("method").GetString());
        Assert.Equal("test-collector", root.GetProperty("params").GetProperty("name").GetString());
    }

    [Fact]
    public void IpcRequest_Serializes_WithNullParams_OmitsParamsField()
    {
        var request = new IpcRequest
        {
            Method = "collector.heartbeat",
            Params = null,
        };

        var json = JsonSerializer.Serialize(request, s_options);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("collector.heartbeat", root.GetProperty("method").GetString());
        Assert.False(root.TryGetProperty("params", out _));
    }

    [Fact]
    public void IpcRequest_Deserializes_Correctly()
    {
        var json = """{"method":"collector.submit","params":{"events":[]}}""";
        var request = JsonSerializer.Deserialize<IpcRequest>(json, s_options);

        Assert.NotNull(request);
        Assert.Equal("collector.submit", request!.Method);
        Assert.NotNull(request.Params);
    }

    // ── IpcResponse tests ───────────────────────────────────────────────────

    [Fact]
    public void IpcResponse_WithSuccessResult_Deserializes_Correctly()
    {
        var json = """{"result":{"collector_id":"abc-123","max_batch_size":100}}""";
        var response = JsonSerializer.Deserialize<IpcResponse>(json, s_options);

        Assert.NotNull(response);
        Assert.False(response!.IsError);
        Assert.Null(response.Error);
        Assert.NotNull(response.Result);
        Assert.Equal("abc-123", response.Result!["collector_id"]!.GetValue<string>());
        Assert.Equal(100, response.Result["max_batch_size"]!.GetValue<int>());
    }

    [Fact]
    public void IpcResponse_WithError_Deserializes_AndIsErrorReturnsTrue()
    {
        var json = """{"error":{"code":-32600,"message":"Invalid request"}}""";
        var response = JsonSerializer.Deserialize<IpcResponse>(json, s_options);

        Assert.NotNull(response);
        Assert.True(response!.IsError);
        Assert.NotNull(response.Error);
        Assert.Equal(-32600, response.Error!.Code);
        Assert.Equal("Invalid request", response.Error.Message);
        Assert.Null(response.Result);
    }

    [Fact]
    public void IpcResponse_WithoutError_IsErrorReturnsFalse()
    {
        var response = new IpcResponse
        {
            Result = new JsonObject { ["ok"] = true },
            Error = null,
        };

        Assert.False(response.IsError);
    }

    [Fact]
    public void IpcResponse_WithError_IsErrorReturnsTrue()
    {
        var response = new IpcResponse
        {
            Result = null,
            Error = new IpcError { Code = 500, Message = "Internal error" },
        };

        Assert.True(response.IsError);
    }

    [Fact]
    public void IpcResponse_Default_HasNoErrorAndNoResult()
    {
        var response = new IpcResponse();

        Assert.False(response.IsError);
        Assert.Null(response.Error);
        Assert.Null(response.Result);
    }

    [Fact]
    public void IpcError_DefaultMessage_IsEmptyString()
    {
        var error = new IpcError();
        Assert.Equal(string.Empty, error.Message);
        Assert.Equal(0, error.Code);
    }

    // ── Roundtrip tests ─────────────────────────────────────────────────────

    [Fact]
    public void IpcRequest_Roundtrips_ThroughSerialization()
    {
        var original = new IpcRequest
        {
            Method = "collector.submit",
            Params = new JsonObject
            {
                ["events"] = new JsonArray(
                    new JsonObject { ["type"] = "agent_heartbeat" }
                ),
            },
        };

        var json = JsonSerializer.Serialize(original, s_options);
        var deserialized = JsonSerializer.Deserialize<IpcRequest>(json, s_options);

        Assert.NotNull(deserialized);
        Assert.Equal("collector.submit", deserialized!.Method);
        Assert.NotNull(deserialized.Params);
    }

    [Fact]
    public void IpcResponse_SuccessRoundtrip()
    {
        var original = new IpcResponse
        {
            Result = new JsonObject { ["accepted"] = 5 },
        };

        var json = JsonSerializer.Serialize(original, s_options);
        var deserialized = JsonSerializer.Deserialize<IpcResponse>(json, s_options);

        Assert.NotNull(deserialized);
        Assert.False(deserialized!.IsError);
        Assert.Equal(5, deserialized.Result!["accepted"]!.GetValue<int>());
    }

    [Fact]
    public void IpcResponse_ErrorRoundtrip()
    {
        var original = new IpcResponse
        {
            Error = new IpcError { Code = 42, Message = "Custom error" },
        };

        var json = JsonSerializer.Serialize(original, s_options);
        var deserialized = JsonSerializer.Deserialize<IpcResponse>(json, s_options);

        Assert.NotNull(deserialized);
        Assert.True(deserialized!.IsError);
        Assert.Equal(42, deserialized.Error!.Code);
        Assert.Equal("Custom error", deserialized.Error.Message);
    }
}
