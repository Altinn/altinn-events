using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

namespace Altinn.Platform.Events.Repository;

/// <inheritdoc/>
[ExcludeFromCodeCoverage]
public class MetricsRepository(NpgsqlDataSource dataSource) : IMetricsRepository
{
    private readonly string _getBacklogMetricsSql = "select registeredcount, registeredoldestageseconds, retryexhaustedcount from events.get_backlog_metrics()";

    private readonly NpgsqlDataSource _dataSource = dataSource;

    /// <inheritdoc/>
    public async Task<(long RegisteredCount, double RegisteredOldestAgeSeconds, long RetryExhaustedCount)> GetBacklogMetrics(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getBacklogMetricsSql);
        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return (0, 0, 0);
        }

        long registeredCount = await reader.IsDBNullAsync(0, cancellationToken) ? 0 : reader.GetInt64(0);
        double registeredOldestAgeSeconds = await reader.IsDBNullAsync(1, cancellationToken) ? 0 : reader.GetDouble(1);
        long retryExhaustedCount = await reader.IsDBNullAsync(2, cancellationToken) ? 0 : reader.GetInt64(2);
        return (registeredCount, registeredOldestAgeSeconds, retryExhaustedCount);
    }
}
