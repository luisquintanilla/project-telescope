using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Telescope.Aspire.Hosting;

/// <summary>
/// Extension methods for adding Project Telescope to an Aspire AppHost.
/// </summary>
public static class TelescopeBuilderExtensions
{
    /// <summary>
    /// Adds a Project Telescope service to the application model.
    /// Starts <c>tele service start --foreground</c> as a managed executable resource.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="pipeName">Optional custom pipe name (default: "telescope-collector").</param>
    /// <returns>A resource builder for further configuration.</returns>
    /// <remarks>
    /// The <c>tele</c> CLI must be installed and available on PATH.
    /// Install Project Telescope from https://github.com/microsoft/project-telescope/releases
    /// </remarks>
    public static IResourceBuilder<TelescopeResource> AddTelescope(
        this IDistributedApplicationBuilder builder,
        string name,
        string? pipeName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var resource = new TelescopeResource(name);
        if (pipeName is not null)
        {
            resource.PipeName = pipeName;
        }

        return builder.AddResource(resource)
            .WithArgs("service", "start", "--foreground");
    }

    /// <summary>
    /// Adds the Project Telescope Dashboard as a standalone executable resource.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name (default: "telescope-dashboard").</param>
    /// <returns>A resource builder for further configuration.</returns>
    public static IResourceBuilder<ExecutableResource> AddTelescopeDashboard(
        this IDistributedApplicationBuilder builder,
        string name = "telescope-dashboard")
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddExecutable(name, "telescope-dashboard", ".");
    }
}
