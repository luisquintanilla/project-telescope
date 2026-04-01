using System.IO.Pipes;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Telescope.Aspire.Client;

/// <summary>
/// Health check that verifies the Telescope service is accessible
/// via named pipe. Returns <see cref="HealthStatus.Degraded"/> (not Unhealthy)
/// when unavailable — Telescope is optional, the app works without it.
/// </summary>
internal sealed class TelescopeHealthCheck(string pipeName) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            await pipe.ConnectAsync(cts.Token).ConfigureAwait(false);
            return HealthCheckResult.Healthy("Telescope service is running.");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded("Telescope service connection timed out — telemetry will be silently skipped.");
        }
        catch
        {
            return HealthCheckResult.Degraded("Telescope service is not available — telemetry will be silently skipped.");
        }
    }
}
