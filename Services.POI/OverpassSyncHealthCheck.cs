using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Services.POI;

internal sealed class OverpassSyncHealthCheck(OverpassSyncStatus status) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(status.IsComplete
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Waiting for the initial Overpass store sync to complete"));
}
