using Microsoft.Extensions.AI;
using Telescope.Extensions.AI.ChatCompletion;
using Telescope.Extensions.AI.Transport;
using Xunit;

namespace Telescope.Extensions.AI.Tests.ChatCompletion;

public class TelescopeChatClientTests : IDisposable
{
    private readonly StubChatClient _innerClient = new();
    private readonly TelescopeChatClientOptions _options = new()
    {
        AgentId = "test-agent",
        AgentName = "Test Agent",
        Transport = new TelescopeTransportOptions
        {
            PipeName = "telescope-test-nonexistent-" + Guid.NewGuid().ToString("N"),
            ConnectTimeout = TimeSpan.FromMilliseconds(100),
            MaxConnectRetries = 1,
        },
    };

    public void Dispose()
    {
        _innerClient.Dispose();
    }

    [Fact]
    public async Task GetResponseAsync_PassesThrough_InnerClientResponse()
    {
        var expected = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response"));
        _innerClient.NextResponse = expected;

        using var client = new TelescopeChatClient(_innerClient, _options);

        var messages = new[] { new ChatMessage(ChatRole.User, "Hello") };
        var result = await client.GetResponseAsync(messages);

        Assert.NotNull(result);
        Assert.Single(result.Messages);
        Assert.Equal("Test response", result.Messages[0].Text);
    }

    [Fact]
    public async Task GetResponseAsync_DoesNotThrow_WhenTelescopeUnavailable()
    {
        var expected = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Works fine"));
        _innerClient.NextResponse = expected;

        using var client = new TelescopeChatClient(_innerClient, _options);

        var messages = new[] { new ChatMessage(ChatRole.User, "Hello") };

        // Should not throw even though Telescope pipe doesn't exist
        var result = await client.GetResponseAsync(messages);
        Assert.Equal("Works fine", result.Messages[0].Text);
    }

    [Fact]
    public async Task GetResponseAsync_RethrowsInnerClientExceptions()
    {
        _innerClient.ExceptionToThrow = new InvalidOperationException("Inner client error");

        using var client = new TelescopeChatClient(_innerClient, _options);

        var messages = new[] { new ChatMessage(ChatRole.User, "Hello") };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetResponseAsync(messages));
        Assert.Equal("Inner client error", ex.Message);
    }

    [Fact]
    public async Task GetResponseAsync_PreservesResponseRole()
    {
        var expected = new ChatResponse(new ChatMessage(ChatRole.Assistant, "I'm an assistant"));
        _innerClient.NextResponse = expected;

        using var client = new TelescopeChatClient(_innerClient, _options);

        var messages = new[] { new ChatMessage(ChatRole.User, "Who are you?") };
        var result = await client.GetResponseAsync(messages);

        Assert.Equal(ChatRole.Assistant, result.Messages[0].Role);
    }

    [Fact]
    public async Task GetResponseAsync_MultipleCallsSucceed()
    {
        _innerClient.NextResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Reply 1"));

        using var client = new TelescopeChatClient(_innerClient, _options);

        var messages = new[] { new ChatMessage(ChatRole.User, "Call 1") };
        var result1 = await client.GetResponseAsync(messages);
        Assert.Equal("Reply 1", result1.Messages[0].Text);

        _innerClient.NextResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Reply 2"));
        var result2 = await client.GetResponseAsync(messages);
        Assert.Equal("Reply 2", result2.Messages[0].Text);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_DoesNotThrow_WhenTelescopeUnavailable()
    {
        using var client = new TelescopeChatClient(_innerClient, _options);

        var messages = new[] { new ChatMessage(ChatRole.User, "Stream me") };

        // Should not throw even though Telescope pipe doesn't exist
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(messages))
        {
            updates.Add(update);
        }

        // StubChatClient returns empty stream, so no updates expected
        Assert.Empty(updates);
    }

    [Fact]
    public void Constructor_ThrowsOnNullOptions()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TelescopeChatClient(_innerClient, null!));
    }

    /// <summary>
    /// Stub implementation of IChatClient for testing middleware behavior.
    /// </summary>
    private sealed class StubChatClient : IChatClient
    {
        public ChatResponse? NextResponse { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(
                NextResponse ?? new ChatResponse(new ChatMessage(ChatRole.Assistant, "Default")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return AsyncEnumerable.Empty<ChatResponseUpdate>();
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
