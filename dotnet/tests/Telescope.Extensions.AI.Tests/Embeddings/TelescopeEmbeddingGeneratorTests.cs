using Microsoft.Extensions.AI;
using Telescope.Extensions.AI.Embeddings;
using Telescope.Extensions.AI.Transport;
using Xunit;

namespace Telescope.Extensions.AI.Tests.Embeddings;

public class TelescopeEmbeddingGeneratorTests : IDisposable
{
    private readonly StubEmbeddingGenerator _innerGenerator = new();
    private readonly TelescopeEmbeddingGeneratorOptions _options = new()
    {
        AgentId = "test-embedding-agent",
        AgentName = "Test Embedding Agent",
        Transport = new TelescopeTransportOptions
        {
            PipeName = "telescope-test-embed-nonexistent-" + Guid.NewGuid().ToString("N"),
            ConnectTimeout = TimeSpan.FromMilliseconds(100),
            MaxConnectRetries = 1,
        },
    };

    public void Dispose()
    {
        _innerGenerator.Dispose();
    }

    [Fact]
    public async Task GenerateAsync_PassesThrough_InnerGeneratorResult()
    {
        var expectedEmbedding = new Embedding<float>(new float[] { 0.1f, 0.2f, 0.3f });
        _innerGenerator.NextResult = new GeneratedEmbeddings<Embedding<float>>([expectedEmbedding]);

        using var generator = new TelescopeEmbeddingGenerator(_innerGenerator, _options);

        var result = await generator.GenerateAsync(["test input"]);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(3, result[0].Vector.Length);
        Assert.Equal(0.1f, result[0].Vector.Span[0]);
        Assert.Equal(0.2f, result[0].Vector.Span[1]);
        Assert.Equal(0.3f, result[0].Vector.Span[2]);
    }

    [Fact]
    public async Task GenerateAsync_DoesNotThrow_WhenTelescopeUnavailable()
    {
        var expectedEmbedding = new Embedding<float>(new float[] { 1.0f, 2.0f });
        _innerGenerator.NextResult = new GeneratedEmbeddings<Embedding<float>>([expectedEmbedding]);

        using var generator = new TelescopeEmbeddingGenerator(_innerGenerator, _options);

        // Should not throw even though Telescope pipe doesn't exist
        var result = await generator.GenerateAsync(["hello world"]);

        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public async Task GenerateAsync_RethrowsInnerGeneratorExceptions()
    {
        _innerGenerator.ExceptionToThrow = new InvalidOperationException("Embedding service down");

        using var generator = new TelescopeEmbeddingGenerator(_innerGenerator, _options);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateAsync(["test"]));
        Assert.Equal("Embedding service down", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_MultipleInputs_PassesThrough()
    {
        var embeddings = new GeneratedEmbeddings<Embedding<float>>(
        [
            new Embedding<float>(new float[] { 0.1f }),
            new Embedding<float>(new float[] { 0.2f }),
            new Embedding<float>(new float[] { 0.3f }),
        ]);
        _innerGenerator.NextResult = embeddings;

        using var generator = new TelescopeEmbeddingGenerator(_innerGenerator, _options);

        var result = await generator.GenerateAsync(["a", "b", "c"]);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Constructor_ThrowsOnNullOptions()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TelescopeEmbeddingGenerator(_innerGenerator, null!));
    }

    /// <summary>
    /// Stub implementation of IEmbeddingGenerator for testing middleware behavior.
    /// </summary>
    private sealed class StubEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public GeneratedEmbeddings<Embedding<float>>? NextResult { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        public EmbeddingGeneratorMetadata Metadata { get; } = new("test-provider");

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(
                NextResult ?? new GeneratedEmbeddings<Embedding<float>>(
                    [new Embedding<float>(new float[] { 0.0f })]));
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            if (serviceType == typeof(EmbeddingGeneratorMetadata))
            {
                return Metadata;
            }

            return null;
        }

        public void Dispose() { }
    }
}
