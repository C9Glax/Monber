using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Services.Prices;

internal sealed class StoreSyncHealthCheck(StoreSyncStatus status) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(status.IsComplete
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Waiting for the initial store sync to complete"));
}
