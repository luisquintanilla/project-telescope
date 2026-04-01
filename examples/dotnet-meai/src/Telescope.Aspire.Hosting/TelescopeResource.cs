using Aspire.Hosting.ApplicationModel;

namespace Telescope.Aspire.Hosting;

/// <summary>
/// Represents a Project Telescope service resource managed by Aspire.
/// Telescope is a local-first AI agent observability tool that communicates
/// via named pipes — there are no HTTP endpoints.
/// </summary>
public class TelescopeResource(string name) : ExecutableResource(name, "tele", "."), IResourceWithConnectionString
{
    internal const string DefaultPipeName = "telescope-collector";

    /// <summary>
    /// Named pipe used for collector IPC communication.
    /// </summary>
    public string PipeName { get; set; } = DefaultPipeName;

    /// <summary>
    /// Connection string is the pipe name — client apps use this
    /// to know which named pipe to connect to.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{PipeName}");
}
