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
    private readonly string _getRetryExhaustedCountSql = "select events.get_retry_exhausted_count()";
    private readonly string _getRegisteredEventsMetricsSql = "select registeredcount, oldestageseconds from events.get_registered_events_metrics()";

    private readonly NpgsqlDataSource _dataSource = dataSource;

    /// <inheritdoc/>
    public async Task<long> GetRetryExhaustedCount(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getRetryExhaustedCountSql);

        object result = await pgcom.ExecuteScalarAsync(cancellationToken);

        return result is null or DBNull ? 0 : (long)result;
    }

    /// <inheritdoc/>
    public async Task<(long Count, double OldestAgeSeconds)> GetRegisteredEventsMetrics(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getRegisteredEventsMetricsSql);
        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return (0, 0);
        }

        long count = await reader.IsDBNullAsync(0, cancellationToken) ? 0 : reader.GetInt64(0);
        double oldestAgeSeconds = await reader.IsDBNullAsync(1, cancellationToken) ? 0 : reader.GetDouble(1);
        return (count, oldestAgeSeconds);
    }
}
