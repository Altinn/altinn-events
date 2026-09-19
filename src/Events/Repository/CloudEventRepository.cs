using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Platform.Events.Repository;

/// <summary>
/// Handles events repository.
/// </summary>
[ExcludeFromCodeCoverage]
public class CloudEventRepository : ICloudEventRepository
{
    private readonly string _insertEventSql = @"insert into events.events(cloudevent, idempotencykey) VALUES ($1, $2)
            ON CONFLICT DO NOTHING
            RETURNING sequenceno";

    private readonly string _getAppEventsSql = "select events.getappevents_v2(@_subject, @_after, @_from, @_to, @_type, @_source, @_resource, @_size)";
    private readonly string _getEventsSql = "select events.getevents_v2($1, $2, $3, $4, $5, $6)"; // _resource, _subject, _alternativesubject, _after, _type, _size
    private readonly string _claimRegisteredEventSql = "select * from events.claim_registered_event()";
    private readonly string _markEventProcessedSql = "update events.events set status = 'processed' where sequenceno = $1";
    private readonly string _markEventRetrySql = @"update events.events
    set retrycount = retrycount + 1,
        lastretried = now(),
        status = case when retrycount + 1 >= @maxretrycount then 'retryExhausted' else status end
    where sequenceno = @sequenceno";

    private readonly NpgsqlDataSource _dataSource;
    private readonly EventsProcessingSettings _eventsProcessingSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudEventRepository"/> class.
    /// </summary>
    public CloudEventRepository(NpgsqlDataSource dataSource, IOptions<EventsProcessingSettings> eventsProcessingSettings)
    {
        _dataSource = dataSource;
        _eventsProcessingSettings = eventsProcessingSettings.Value;
    }

    /// <inheritdoc/>
    public async Task<bool> CreateEvent(string cloudEvent, Guid? idempotencyKey)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertEventSql);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Jsonb, cloudEvent);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, idempotencyKey.HasValue ? idempotencyKey.Value : DBNull.Value);

        object result = await pgcom.ExecuteScalarAsync();

        // A row is returned only when the insert actually happened.
        // ON CONFLICT DO NOTHING (with no explicit target) suppresses errors from
        // *any* unique constraint violation, covering both the existing
        // (cloudevent -> 'id', cloudevent -> 'source') dedup and the new
        // idempotencykey partial unique index.
        return result != null;
    }

    /// <inheritdoc/>
    public async Task<List<CloudEvent>> GetAppEvents(string after, DateTime? from, DateTime? to, string subject, List<string> source, string resource, List<string> type, int size)
    {
        List<CloudEvent> searchResult = new();

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getAppEventsSql);

        // ignore missing [Flags] attribute on NpgsqlDbType enum.
        // For more info: https://github.com/npgsql/npgsql/issues/2801
#pragma warning disable S3265
        pgcom.Parameters.AddWithValue("_subject", NpgsqlDbType.Varchar, subject ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue("_after", NpgsqlDbType.Varchar, after);
        pgcom.Parameters.AddWithValue("_from", NpgsqlDbType.TimestampTz, from ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue("_to", NpgsqlDbType.TimestampTz, to ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue("_type", NpgsqlDbType.Array | NpgsqlDbType.Text, type ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue("_source", NpgsqlDbType.Array | NpgsqlDbType.Text, source ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue("_resource", NpgsqlDbType.Text, resource ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue("_size", NpgsqlDbType.Integer, size);
#pragma warning restore S3265       

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                CloudEvent cloudEvent = DeserializeAndConvertTime(reader[0].ToString());
                searchResult.Add(cloudEvent);
            }
        }

        return searchResult;
    }

    /// <inheritdoc/>
    public async Task<List<CloudEvent>> GetEvents(string resource, string after, string subject, string alternativeSubject, List<string> type, int size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        List<CloudEvent> searchResult = GetSearchResult();

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getEventsSql);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Varchar, resource);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Varchar, subject ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Varchar, alternativeSubject ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Varchar, after);

        // Ignore missing [Flags] attribute on NpgsqlDbType enum.
        // For more info: https://github.com/npgsql/npgsql/issues/2801
#pragma warning disable S3265
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Array | NpgsqlDbType.Text, type ?? (object)DBNull.Value);
#pragma warning restore S3265

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, size);

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                CloudEvent cloudEvent = DeserializeAndConvertTime(reader[0].ToString());
                searchResult.Add(cloudEvent);
            }
        }

        return searchResult;
    }

    /// <inheritdoc/>
    public async Task<ClaimedEvent> ClaimRegisteredEventAsync(UnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        NpgsqlCommand pgcom = unitOfWork.Connection.CreateCommand();
        pgcom.CommandText = _claimRegisteredEventSql;
        pgcom.Transaction = unitOfWork.Transaction;

        await using (pgcom)
        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new ClaimedEvent
            {
                SequenceNo = reader.GetInt64(0),
                CloudEvent = DeserializeAndConvertTime(reader.GetString(1))
            };
        }
    }

    /// <inheritdoc/>
    public async Task MarkEventProcessedAsync(UnitOfWork unitOfWork, long sequenceNo, CancellationToken cancellationToken)
    {
        NpgsqlCommand pgcom = unitOfWork.Connection.CreateCommand();
        pgcom.CommandText = _markEventProcessedSql;
        pgcom.Transaction = unitOfWork.Transaction;
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Bigint, sequenceNo);

        await using (pgcom)
        {
            await pgcom.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task MarkEventRetryAsync(UnitOfWork unitOfWork, long sequenceNo, CancellationToken cancellationToken)
    {
        NpgsqlCommand pgcom = unitOfWork.Connection.CreateCommand();
        pgcom.CommandText = _markEventRetrySql;
        pgcom.Transaction = unitOfWork.Transaction;
        pgcom.Parameters.AddWithValue("sequenceno", NpgsqlDbType.Bigint, sequenceNo);
        pgcom.Parameters.AddWithValue("maxretrycount", NpgsqlDbType.Integer, _eventsProcessingSettings.MaxRetryCount);

        await using (pgcom)
        {
            await pgcom.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static List<CloudEvent> GetSearchResult()
    {
        return new List<CloudEvent>();
    }

    private static CloudEvent DeserializeAndConvertTime(string eventString)
    {
        var formatter = new CloudNative.CloudEvents.SystemTextJson.JsonEventFormatter();
        CloudEvent cloudEvent = formatter.DecodeStructuredModeMessage(new MemoryStream(Encoding.UTF8.GetBytes(eventString)), null, null);

        if (cloudEvent.Time != null)
        {
            cloudEvent.Time = cloudEvent.Time.Value.ToUniversalTime();
        }

        return cloudEvent;
    }
}
