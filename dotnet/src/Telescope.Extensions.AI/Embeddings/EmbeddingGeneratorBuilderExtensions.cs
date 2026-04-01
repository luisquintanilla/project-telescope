using Microsoft.Extensions.AI;

namespace Telescope.Extensions.AI.Embeddings;

/// <summary>
/// Extension methods for adding Telescope observability to an embedding generator pipeline.
/// </summary>
public static class EmbeddingGeneratorBuilderExtensions
{
    /// <summary>
    /// Adds the Telescope observability middleware to the embedding generator pipeline.
    /// </summary>
    public static EmbeddingGeneratorBuilder<string, Embedding<float>> UseTelescope(
        this EmbeddingGeneratorBuilder<string, Embedding<float>> builder,
        Action<TelescopeEmbeddingGeneratorOptions> configure)
    {
        var options = new TelescopeEmbeddingGeneratorOptions { AgentId = "", AgentName = "" };
        configure(options);
        return builder.Use(inner => new TelescopeEmbeddingGenerator(inner, options));
    }
}
