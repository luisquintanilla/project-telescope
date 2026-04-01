using System.Text.Json;
using System.Text.Json.Nodes;
using Telescope.Extensions.AI.Events;
using Xunit;

namespace Telescope.Extensions.AI.Tests.Events;

public class EventSerializationTests
{
    // ── Type discriminator tests ────────────────────────────────────────────

    [Fact]
    public void AgentDiscoveredEvent_Serializes_WithCorrectTypeDiscriminator()
    {
        var agentId = Guid.NewGuid();
        EventKind evt = new AgentDiscoveredEvent(
            AgentId: agentId,
            Name: "test-agent",
            AgentType: "ai-assistant",
            Version: "1.0.0");

        var json = EventSerializer.Serialize(evt);
        var doc = JsonDocument.Parse(json);

        Assert.Equal("agent_discovered", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("test-agent", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("ai-assistant", doc.RootElement.GetProperty("agent_type").GetString());
        Assert.Equal("1.0.0", doc.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public void ToolCallStartedEvent_Serializes_WithSnakeCasePropertyNames()
    {
        var turnId = Guid.NewGuid();
        var effectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        EventKind evt = new ToolCallStartedEvent(
            TurnId: turnId,
            EffectId: effectId,
            Name: "read_file",
            SessionId: sessionId);

        var json = EventSerializer.Serialize(evt);
        var doc = JsonDocument.Parse(json);

        Assert.Equal("tool_call_started", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("read_file", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal(turnId.ToString(), doc.RootElement.GetProperty("turn_id").GetString());
        Assert.Equal(effectId.ToString(), doc.RootElement.GetProperty("effect_id").GetString());
        Assert.Equal(sessionId.ToString(), doc.RootElement.GetProperty("session_id").GetString());
    }

    [Fact]
    public void TurnCompletedEvent_Serializes_WithOptionalNullFieldsOmitted()
    {
        var sessionId = Guid.NewGuid();
        var turnId = Guid.NewGuid();
        EventKind evt = new TurnCompletedEvent(
            SessionId: sessionId,
            TurnId: turnId,
            Status: "completed");

        var json = EventSerializer.Serialize(evt);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("turn_completed", root.GetProperty("type").GetString());
        Assert.Equal("completed", root.GetProperty("status").GetString());

        // Optional nullable fields should be omitted
        Assert.False(root.TryGetProperty("user_message", out _));
        Assert.False(root.TryGetProperty("assistant_response", out _));
        Assert.False(root.TryGetProperty("model_name", out _));
        Assert.False(root.TryGetProperty("tokens", out _));
        Assert.False(root.TryGetProperty("duration_ms", out _));
        Assert.False(root.TryGetProperty("turn_index", out _));
    }

    [Fact]
    public void TurnCompletedEvent_Serializes_WithAllFieldsPopulated()
    {
        var sessionId = Guid.NewGuid();
        var turnId = Guid.NewGuid();
        var tokensNode = new JsonObject { ["input"] = 100, ["output"] = 200 };
        EventKind evt = new TurnCompletedEvent(
            SessionId: sessionId,
            TurnId: turnId,
            Status: "completed",
            TurnIndex: 3,
            UserMessage: "Hello",
            AssistantResponse: "Hi there",
            ModelName: "gpt-4",
            Tokens: tokensNode,
            DurationMs: 1500);

        var json = EventSerializer.Serialize(evt);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Hello", root.GetProperty("user_message").GetString());
        Assert.Equal("Hi there", root.GetProperty("assistant_response").GetString());
        Assert.Equal("gpt-4", root.GetProperty("model_name").GetString());
        Assert.Equal(3u, root.GetProperty("turn_index").GetUInt32());
        Assert.Equal(1500u, root.GetProperty("duration_ms").GetUInt32());
        Assert.Equal(100, root.GetProperty("tokens").GetProperty("input").GetInt32());
        Assert.Equal(200, root.GetProperty("tokens").GetProperty("output").GetInt32());
    }

    [Fact]
    public void CustomEvent_Serializes_WithEventTypeAndData()
    {
        var data = new JsonObject { ["key"] = "value", ["count"] = 42 };
        EventKind evt = new CustomEvent(EventType: "my_custom_event", Data: data);

        var json = EventSerializer.Serialize(evt);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("custom", root.GetProperty("type").GetString());
        Assert.Equal("my_custom_event", root.GetProperty("event_type").GetString());
        Assert.Equal("value", root.GetProperty("data").GetProperty("key").GetString());
        Assert.Equal(42, root.GetProperty("data").GetProperty("count").GetInt32());
    }

    [Fact]
    public void SessionStartedEvent_Serializes_Correctly()
    {
        var sessionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        EventKind evt = new SessionStartedEvent(
            SessionId: sessionId,
            AgentId: agentId,
            Cwd: "/home/user/project",
            GitRepo: "owner/repo",
            GitBranch: "main");

        var json = EventSerializer.Serialize(evt);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("session_started", root.GetProperty("type").GetString());
        Assert.Equal(sessionId.ToString(), root.GetProperty("session_id").GetString());
        Assert.Equal(agentId.ToString(), root.GetProperty("agent_id").GetString());
        Assert.Equal("/home/user/project", root.GetProperty("cwd").GetString());
        Assert.Equal("owner/repo", root.GetProperty("git_repo").GetString());
        Assert.Equal("main", root.GetProperty("git_branch").GetString());
    }

    [Fact]
    public void SessionStartedEvent_OmitsNullOptionalFields()
    {
        var sessionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        EventKind evt = new SessionStartedEvent(SessionId: sessionId, AgentId: agentId);

        var json = EventSerializer.Serialize(evt);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("cwd", out _));
        Assert.False(root.TryGetProperty("git_repo", out _));
        Assert.False(root.TryGetProperty("git_branch", out _));
    }

    [Fact]
    public void ErrorOccurredEvent_Serializes_Correctly()
    {
        var turnId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        EventKind evt = new ErrorOccurredEvent(
            Message: "Something went wrong",
            TurnId: turnId,
            SessionId: sessionId,
            Category: "chat_completion");

        var json = EventSerializer.Serialize(evt);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("error_occurred", root.GetProperty("type").GetString());
        Assert.Equal("Something went wrong", root.GetProperty("message").GetString());
        Assert.Equal("chat_completion", root.GetProperty("category").GetString());
    }

    [Fact]
    public void TokenUsageReportedEvent_Serializes_Correctly()
    {
        var turnId = Guid.NewGuid();
        EventKind evt = new TokenUsageReportedEvent(
            TurnId: turnId,
            InputTokens: 500,
            OutputTokens: 200,
            CacheReadTokens: 100);

        var json = EventSerializer.Serialize(evt);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("token_usage_reported", root.GetProperty("type").GetString());
        Assert.Equal(500ul, root.GetProperty("input_tokens").GetUInt64());
        Assert.Equal(200ul, root.GetProperty("output_tokens").GetUInt64());
        Assert.Equal(100ul, root.GetProperty("cache_read_tokens").GetUInt64());
    }

    [Fact]
    public void TokenUsageReportedEvent_OmitsNullTokenFields()
    {
        var turnId = Guid.NewGuid();
        EventKind evt = new TokenUsageReportedEvent(TurnId: turnId, InputTokens: 500);

        var json = EventSerializer.Serialize(evt);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("input_tokens", out _));
        Assert.False(root.TryGetProperty("output_tokens", out _));
        Assert.False(root.TryGetProperty("cache_read_tokens", out _));
    }

    [Fact]
    public void ModelUsedEvent_Serializes_Correctly()
    {
        var sessionId = Guid.NewGuid();
        EventKind evt = new ModelUsedEvent(
            SessionId: sessionId,
            Name: "gpt-4o",
            Provider: "https://api.openai.com");

        var json = EventSerializer.Serialize(evt);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("model_used", root.GetProperty("type").GetString());
        Assert.Equal("gpt-4o", root.GetProperty("name").GetString());
        Assert.Equal("https://api.openai.com", root.GetProperty("provider").GetString());
    }

    [Fact]
    public void UserMessageEvent_Serializes_Correctly()
    {
        var sessionId = Guid.NewGuid();
        var turnId = Guid.NewGuid();
        EventKind evt = new UserMessageEvent(
            SessionId: sessionId,
            TurnId: turnId,
            Content: "Write a hello world program");

        var json = EventSerializer.Serialize(evt);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("user_message", root.GetProperty("type").GetString());
        Assert.Equal("Write a hello world program", root.GetProperty("content").GetString());
    }

    [Fact]
    public void ToolCallCompletedEvent_Serializes_Correctly()
    {
        var effectId = Guid.NewGuid();
        var resultNode = JsonNode.Parse("{\"output\": \"file contents\"}")!;
        EventKind evt = new ToolCallCompletedEvent(
            EffectId: effectId,
            Status: "completed",
            Result: resultNode,
            DurationMs: 42);

        var json = EventSerializer.Serialize(evt);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("tool_call_completed", root.GetProperty("type").GetString());
        Assert.Equal("completed", root.GetProperty("status").GetString());
        Assert.Equal(42u, root.GetProperty("duration_ms").GetUInt32());
        Assert.Equal("file contents", root.GetProperty("result").GetProperty("output").GetString());
    }

    // ── GUID format tests ───────────────────────────────────────────────────

    [Fact]
    public void GuidFields_Serialize_AsStandardFormat()
    {
        var agentId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        EventKind evt = new AgentHeartbeatEvent(AgentId: agentId);

        var json = EventSerializer.Serialize(evt);
        var doc = JsonDocument.Parse(json);

        var serializedGuid = doc.RootElement.GetProperty("agent_id").GetString();
        Assert.Equal("12345678-1234-1234-1234-123456789abc", serializedGuid);
    }

    // ── Deserialization roundtrip tests ──────────────────────────────────────

    [Fact]
    public void AgentDiscoveredEvent_Roundtrips_ThroughSerialization()
    {
        var agentId = Guid.NewGuid();
        EventKind original = new AgentDiscoveredEvent(
            AgentId: agentId,
            Name: "my-agent",
            AgentType: "ai-assistant",
            ExecutablePath: "/usr/bin/agent",
            Version: "2.0.0");

        var json = EventSerializer.Serialize(original);
        var deserialized = EventSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        var result = Assert.IsType<AgentDiscoveredEvent>(deserialized);
        Assert.Equal(agentId, result.AgentId);
        Assert.Equal("my-agent", result.Name);
        Assert.Equal("ai-assistant", result.AgentType);
        Assert.Equal("/usr/bin/agent", result.ExecutablePath);
        Assert.Equal("2.0.0", result.Version);
    }

    [Fact]
    public void TurnStartedEvent_Roundtrips_ThroughSerialization()
    {
        var sessionId = Guid.NewGuid();
        var turnId = Guid.NewGuid();
        EventKind original = new TurnStartedEvent(
            SessionId: sessionId,
            TurnId: turnId,
            TurnIndex: 7,
            ModelName: "claude-3");

        var json = EventSerializer.Serialize(original);
        var deserialized = EventSerializer.Deserialize(json);

        var result = Assert.IsType<TurnStartedEvent>(deserialized);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(turnId, result.TurnId);
        Assert.Equal(7u, result.TurnIndex);
        Assert.Equal("claude-3", result.ModelName);
    }

    [Fact]
    public void ErrorOccurredEvent_Roundtrips_ThroughSerialization()
    {
        EventKind original = new ErrorOccurredEvent(
            Message: "timeout error",
            Category: "network");

        var json = EventSerializer.Serialize(original);
        var deserialized = EventSerializer.Deserialize(json);

        var result = Assert.IsType<ErrorOccurredEvent>(deserialized);
        Assert.Equal("timeout error", result.Message);
        Assert.Equal("network", result.Category);
        Assert.Null(result.TurnId);
        Assert.Null(result.SessionId);
    }

    [Fact]
    public void CustomEvent_Roundtrips_ThroughSerialization()
    {
        var data = new JsonObject { ["foo"] = "bar" };
        EventKind original = new CustomEvent(EventType: "special_event", Data: data);

        var json = EventSerializer.Serialize(original);
        var deserialized = EventSerializer.Deserialize(json);

        var result = Assert.IsType<CustomEvent>(deserialized);
        Assert.Equal("special_event", result.EventType);
        Assert.NotNull(result.Data);
        Assert.Equal("bar", result.Data!["foo"]!.GetValue<string>());
    }

    [Fact]
    public void SessionEndedEvent_Roundtrips_ThroughSerialization()
    {
        var sessionId = Guid.NewGuid();
        EventKind original = new SessionEndedEvent(
            SessionId: sessionId,
            Status: "completed",
            DurationMs: 12345);

        var json = EventSerializer.Serialize(original);
        var deserialized = EventSerializer.Deserialize(json);

        var result = Assert.IsType<SessionEndedEvent>(deserialized);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal("completed", result.Status);
        Assert.Equal(12345u, result.DurationMs);
    }

    [Fact]
    public void ShellCommandStartedEvent_Roundtrips_ThroughSerialization()
    {
        var turnId = Guid.NewGuid();
        var effectId = Guid.NewGuid();
        EventKind original = new ShellCommandStartedEvent(
            TurnId: turnId,
            EffectId: effectId,
            Command: "dotnet build",
            Cwd: "/src");

        var json = EventSerializer.Serialize(original);
        var deserialized = EventSerializer.Deserialize(json);

        var result = Assert.IsType<ShellCommandStartedEvent>(deserialized);
        Assert.Equal(turnId, result.TurnId);
        Assert.Equal(effectId, result.EffectId);
        Assert.Equal("dotnet build", result.Command);
        Assert.Equal("/src", result.Cwd);
    }

    [Fact]
    public void ConfidenceAssessedEvent_Roundtrips_ThroughSerialization()
    {
        var turnId = Guid.NewGuid();
        EventKind original = new ConfidenceAssessedEvent(
            TurnId: turnId,
            Subject: "code review",
            Level: 0.85);

        var json = EventSerializer.Serialize(original);
        var deserialized = EventSerializer.Deserialize(json);

        var result = Assert.IsType<ConfidenceAssessedEvent>(deserialized);
        Assert.Equal(turnId, result.TurnId);
        Assert.Equal("code review", result.Subject);
        Assert.Equal(0.85, result.Level);
    }

    [Fact]
    public void DecisionMadeEvent_Roundtrips_WithAlternativesList()
    {
        var turnId = Guid.NewGuid();
        EventKind original = new DecisionMadeEvent(
            TurnId: turnId,
            Decision: "Use Redis",
            Reasoning: "Better performance",
            Alternatives: ["Memcached", "In-memory"]);

        var json = EventSerializer.Serialize(original);
        var deserialized = EventSerializer.Deserialize(json);

        var result = Assert.IsType<DecisionMadeEvent>(deserialized);
        Assert.Equal("Use Redis", result.Decision);
        Assert.Equal("Better performance", result.Reasoning);
        Assert.NotNull(result.Alternatives);
        Assert.Equal(2, result.Alternatives!.Count);
        Assert.Equal("Memcached", result.Alternatives[0]);
        Assert.Equal("In-memory", result.Alternatives[1]);
    }

    // ── Serializer options tests ────────────────────────────────────────────

    [Fact]
    public void EventSerializer_Options_UseSnakeCaseNaming()
    {
        Assert.Equal(JsonNamingPolicy.SnakeCaseLower, EventSerializer.Options.PropertyNamingPolicy);
    }

    [Fact]
    public void EventSerializer_Options_IgnoreNullValues()
    {
        Assert.Equal(
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            EventSerializer.Options.DefaultIgnoreCondition);
    }

    [Fact]
    public void EventSerializer_Options_DoNotWriteIndented()
    {
        Assert.False(EventSerializer.Options.WriteIndented);
    }

    // ── Deserialization from raw JSON ────────────────────────────────────────

    [Fact]
    public void Deserialize_FromRawJson_ReturnsCorrectType()
    {
        var json = """{"type":"session_resumed","session_id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"}""";
        var result = EventSerializer.Deserialize(json);

        var evt = Assert.IsType<SessionResumedEvent>(result);
        Assert.Equal(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), evt.SessionId);
    }

    [Fact]
    public void Deserialize_GitCommitCreatedEvent_FromRawJson()
    {
        var json = """{"type":"git_commit_created","turn_id":"11111111-2222-3333-4444-555555555555","sha":"abc123def","message":"Initial commit"}""";
        var result = EventSerializer.Deserialize(json);

        var evt = Assert.IsType<GitCommitCreatedEvent>(result);
        Assert.Equal("abc123def", evt.Sha);
        Assert.Equal("Initial commit", evt.Message);
    }
}
