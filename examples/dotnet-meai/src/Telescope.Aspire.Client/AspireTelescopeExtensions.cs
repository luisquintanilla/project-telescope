using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Telescope.Aspire.Client;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for registering Telescope services with an Aspire-managed host.
/// </summary>
public static class AspireTelescopeExtensions
{
    internal const string DefaultConfigSectionName = "Aspire:Telescope";

    /// <summary>
    /// Registers Telescope settings in DI for use with the M.E.AI pipeline.
    /// The Aspire connection string provides the named pipe name.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionName">Connection name (matches <c>WithReference()</c> in AppHost).</param>
    /// <param name="configureSettings">Optional settings customization.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddTelescopeClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<TelescopeClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var settings = new TelescopeClientSettings();

        // Bind from config section
        builder.Configuration.GetSection(DefaultConfigSectionName).Bind(settings);

        // Connection string from Aspire → pipe name
        if (builder.Configuration.GetConnectionString(connectionName) is string conn
            && !string.IsNullOrWhiteSpace(conn))
        {
            settings.ParseConnectionString(conn);
        }

        configureSettings?.Invoke(settings);

        // Register settings as singleton
        builder.Services.AddSingleton(settings);

        // Health check — returns Degraded, not Unhealthy (graceful degradation)
        if (!settings.DisableHealthChecks)
        {
            builder.Services.AddHealthChecks()
                .Add(new HealthCheckRegistration(
                    "telescope",
                    _ => new TelescopeHealthCheck(settings.PipeName),
                    failureStatus: HealthStatus.Degraded,
                    tags: null,
                    timeout: settings.HealthCheckTimeout.HasValue
                        ? TimeSpan.FromMilliseconds(settings.HealthCheckTimeout.Value)
                        : null));
        }

        return builder;
    }
}
