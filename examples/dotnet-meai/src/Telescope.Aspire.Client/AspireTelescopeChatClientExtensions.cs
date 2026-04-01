using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Telescope.Aspire.Client;
using Telescope.Extensions.AI.ChatCompletion;
using Telescope.Extensions.AI.Transport;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for adding Telescope observability to an M.E.AI pipeline
/// when using Aspire DI.
/// </summary>
public static class AspireTelescopeChatClientExtensions
{
    /// <summary>
    /// Adds Telescope observability middleware to an M.E.AI <see cref="IChatClient"/> pipeline
    /// using settings from DI (configured via <see cref="AspireTelescopeExtensions.AddTelescopeClient"/>).
    /// </summary>
    /// <remarks>
    /// This is a MIDDLEWARE — it wraps an existing <see cref="IChatClient"/>, unlike providers
    /// (e.g., Ollama) which ARE the client. Place it outermost in the pipeline so it sees all
    /// messages including function calling results.
    /// </remarks>
    public static ChatClientBuilder UseTelescope(this ChatClientBuilder builder, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(services);

        var settings = services.GetRequiredService<TelescopeClientSettings>();

        return builder.Use(inner => new TelescopeChatClient(
            inner,
            new TelescopeChatClientOptions
            {
                AgentId = settings.AgentId,
                AgentName = settings.AgentName,
                AgentVersion = settings.AgentVersion,
                EnableSensitiveData = settings.EnableSensitiveData,
                EmitOpenTelemetrySpans = settings.EmitOpenTelemetrySpans,
                Transport = new TelescopeTransportOptions
                {
                    PipeName = settings.PipeName,
                    MaxConnectRetries = settings.MaxConnectRetries,
                    ConnectTimeout = settings.ConnectTimeout,
                },
            }));
    }
}
