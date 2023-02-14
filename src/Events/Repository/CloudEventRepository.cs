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
        private readonly string insertAppEventSql = "call events.insertappevent(@id, @source, @subject, @type, @time, @cloudevent)";
        private readonly string insertEventSql = "insert into events.events(cloudevent) VALUES ($1);";
        private readonly string getAppEventsSql = "select events.getappevents(@_subject, @_after, @_from, @_to, @_type, @_source, @_size)";
        private readonly string getEventsSql = "select events.getevents(@_subject, @_after, @_type, @_source, @_size)";
        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudEventRepository"/> class.
        /// </summary>
        public CloudEventRepository(IOptions<PostgreSqlSettings> postgresSettings)
        {
            _connectionString = string.Format(
                postgresSettings.Value.ConnectionString,
                postgresSettings.Value.EventsDbPwd);
        }

        /// <inheritdoc/>
        public async Task CreateAppEvent(CloudEvent cloudEvent, string serializedCloudEvent)
        {
            await using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var transaction = await conn.BeginTransactionAsync();
            await using NpgsqlCommand pgcom = new NpgsqlCommand(insertAppEventSql, conn);
            pgcom.Parameters.AddWithValue("id", cloudEvent.Id);
            pgcom.Parameters.AddWithValue("source", cloudEvent.Source.OriginalString);
            pgcom.Parameters.AddWithValue("subject", cloudEvent.Subject);
            pgcom.Parameters.AddWithValue("type", cloudEvent.Type);
            pgcom.Parameters.AddWithValue("time", cloudEvent.Time.Value.ToUniversalTime());
            pgcom.Parameters.Add(new NpgsqlParameter("cloudevent", serializedCloudEvent) { Direction = System.Data.ParameterDirection.Input });

            await pgcom.ExecuteNonQueryAsync();

            await using NpgsqlCommand pgcom2 = new NpgsqlCommand(insertEventSql, conn)
            {
                Parameters =
                {
                    new() { Value = serializedCloudEvent, NpgsqlDbType = NpgsqlDbType.Jsonb }
                }
            };

            await pgcom2.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }

        /// <inheritdoc/>
        public async Task CreateEvent(string cloudEvent)
        {
            await using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await using NpgsqlCommand pgcom = new NpgsqlCommand(insertEventSql, conn)
            {
                Parameters =
                {
                    new() { Value = cloudEvent, NpgsqlDbType = NpgsqlDbType.Jsonb }
                }
            };
            await pgcom.ExecuteNonQueryAsync();
        }

        /// <inheritdoc/>
        public async Task<List<CloudEvent>> GetAppEvents(string after, DateTime? from, DateTime? to, string subject, List<string> source, List<string> type, int size)
        {
            List<CloudEvent> searchResult = new List<CloudEvent>();

            await using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using NpgsqlCommand pgcom = new NpgsqlCommand(getAppEventsSql, conn);
            pgcom.Parameters.AddWithValue("_subject", NpgsqlDbType.Varchar, subject);
            pgcom.Parameters.AddWithValue("_after", NpgsqlDbType.Varchar, after);
            pgcom.Parameters.AddWithValue("_from", NpgsqlDbType.TimestampTz, from ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue("_to", NpgsqlDbType.TimestampTz, to ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue("_type", NpgsqlDbType.Array | NpgsqlDbType.Text, type ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue("_source", NpgsqlDbType.Array | NpgsqlDbType.Text, source ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue("_size", NpgsqlDbType.Integer, size);

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
        public async Task<List<CloudEvent>> GetEvents(string after, List<string> source, List<string> type, string subject, int size)
        {
            List<CloudEvent> searchResult = new List<CloudEvent>();

            await using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using NpgsqlCommand pgcom = new NpgsqlCommand(getEventsSql, conn);
            pgcom.Parameters.AddWithValue("_after", NpgsqlDbType.Varchar, after);
            pgcom.Parameters.AddWithValue("_type", NpgsqlDbType.Array | NpgsqlDbType.Text, type ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue("_source", NpgsqlDbType.Array | NpgsqlDbType.Text, source ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue("_subject", NpgsqlDbType.Varchar, subject);
            pgcom.Parameters.AddWithValue("_size", NpgsqlDbType.Integer, size);

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

        private static CloudEvent DeserializeAndConvertTime(string eventString)
        {
            var formatter = new CloudNative.CloudEvents.SystemTextJson.JsonEventFormatter();
            CloudEvent cloudEvent = formatter.DecodeStructuredModeMessage(new MemoryStream(Encoding.UTF8.GetBytes(eventString)), null, null);

            cloudEvent.Time = cloudEvent.Time.Value.ToUniversalTime();

            return cloudEvent;
        }
    }
}
