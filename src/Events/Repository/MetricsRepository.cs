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

    private readonly NpgsqlDataSource _dataSource = dataSource;

    /// <inheritdoc/>
    public async Task<long> GetRetryExhaustedCount(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getRetryExhaustedCountSql);

        object result = await pgcom.ExecuteScalarAsync(cancellationToken);

        return result is null or DBNull ? 0 : (long)result;
    }
}
