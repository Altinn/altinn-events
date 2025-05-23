using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;

using CloudNative.CloudEvents;

using Microsoft.Extensions.Options;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Platform.Events.Repository
{
    /// <summary>
    /// Handles events repository.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class CloudEventRepository : ICloudEventRepository
    {
        private readonly string _insertEventSql = @"insert into events.events(cloudevent) VALUES ($1)
            ON CONFLICT ((cloudevent -> 'id'), (cloudevent -> 'source')) DO NOTHING";

        private readonly string _getAppEventsSql = "select events.getappevents_v2(@_subject, @_after, @_from, @_to, @_type, @_source, @_resource, @_size)";
        private readonly string _getEventsSql = "select events.getevents($1, $2, $3, $4, $5, $6)"; // _resource, _subject, _alternativesubject, _after, _type, _size

        private readonly NpgsqlDataSource _dataSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudEventRepository"/> class.
        /// </summary>
        public CloudEventRepository(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        /// <inheritdoc/>
        public async Task CreateEvent(string cloudEvent)
        {
            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertEventSql);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Jsonb, cloudEvent);

            await pgcom.ExecuteNonQueryAsync();
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
            List<CloudEvent> searchResult = GetSearchResult();

            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getEventsSql);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Varchar, resource);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Varchar, subject ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Varchar, alternativeSubject ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Varchar, after);
#pragma warning disable S3265

            // ignore missing [Flags] attribute on NpgsqlDbType enum.
            // For more info: https://github.com/npgsql/npgsql/issues/2801
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
}
