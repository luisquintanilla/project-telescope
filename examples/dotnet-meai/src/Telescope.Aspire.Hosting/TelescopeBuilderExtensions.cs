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
    /// Runs <c>tele service start</c> to ensure the service is running,
    /// then exposes the named pipe as a connection string for client apps.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="pipeName">Optional custom pipe name (default: "telescope-collector").</param>
    /// <returns>A resource builder for further configuration.</returns>
    /// <remarks>
    /// <para>
    /// The <c>tele</c> CLI must be installed and available on PATH.
    /// Install Project Telescope from https://github.com/microsoft/project-telescope/releases
    /// </para>
    /// <para>
    /// The Telescope service runs as a background daemon — <c>tele service start</c>
    /// starts it and exits. If the service is already running, the command is a no-op.
    /// Use <c>tele status</c> to verify the service state.
    /// </para>
    /// </remarks>
    public static IResourceBuilder<TelescopeResource> AddTelescope(
        this IDistributedApplicationBuilder builder,
        string name,
        string? pipeName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        ValidateToolOnPath("tele",
            "Project Telescope CLI ('tele') was not found on PATH. " +
            "Install it from: https://github.com/microsoft/project-telescope/releases");

        var resource = new TelescopeResource(name);
        if (pipeName is not null)
        {
            resource.PipeName = pipeName;
        }

        return builder.AddResource(resource)
            .WithArgs("service", "start");
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

        ValidateToolOnPath("telescope-dashboard",
            "Project Telescope Dashboard ('telescope-dashboard') was not found on PATH. " +
            "Install it from: https://github.com/microsoft/project-telescope/releases");

        return builder.AddExecutable(name, "telescope-dashboard", ".");
    }

    private static void ValidateToolOnPath(string tool, string errorMessage)
    {
        try
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var separator = OperatingSystem.IsWindows() ? ';' : ':';
            var extensions = OperatingSystem.IsWindows()
                ? new[] { ".exe", ".cmd", ".bat" }
                : Array.Empty<string>();

            foreach (var dir in pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Directory.Exists(dir))
                    continue;

                // Check exact name (Unix) or with extensions (Windows)
                if (File.Exists(Path.Combine(dir, tool)))
                    return;

                foreach (var ext in extensions)
                {
                    if (File.Exists(Path.Combine(dir, tool + ext)))
                        return;
                }
            }
        }
        catch
        {
            // If PATH scanning fails, don't block — let Aspire try to run it
            return;
        }

        throw new InvalidOperationException(errorMessage);
    }
}
