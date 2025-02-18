using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Altinn.Platform.Events.Repository
{
    /// <inheritdoc/>
    [ExcludeFromCodeCoverage]
    public class TraceLogRepository(IOptions<PostgreSqlSettings> postgresSettings, ILogger<TraceLogRepository> logger) : ITraceLogRepository
    {
        private readonly string _connectionString = string.Format(
                postgresSettings.Value.ConnectionString,
                postgresSettings.Value.EventsDbPwd);

        private readonly string _insertTraceLogSql = @"INSERT INTO events.trace_log(
	cloudeventid, resource, eventtype, consumer, subscriptionid, responsecode, subscriberendpoint, activity)
	VALUES (@cloudeventid, @resource, @eventtype, @consumer, @subscriptionid, @responsecode, @subscriberendpoint, @activity);";
        
        private readonly ILogger<TraceLogRepository> _logger = logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceLog"/> class with a
        /// </summary>
        /// <param name="traceLog">Entity model to be stored to persistence</param>
        /// <returns></returns>
        public async Task CreateTraceLogEntry(TraceLog traceLog)
        {
            try
            {
                await using NpgsqlConnection conn = new(_connectionString);
                await conn.OpenAsync();

                await using NpgsqlCommand pgcom = new(_insertTraceLogSql, conn);

                pgcom.Parameters.AddWithValue("cloudeventid", traceLog.CloudEventId);
                pgcom.Parameters.AddWithValue("resource", traceLog.Resource);
                pgcom.Parameters.AddWithValue("eventtype", traceLog.EventType);
                pgcom.Parameters.AddWithValue("activity", traceLog.Activity.ToString());

                // nullable values
                pgcom.Parameters.AddWithValue("consumer", traceLog.Consumer ?? (object)DBNull.Value);
                pgcom.Parameters.AddWithValue("subscriptionid", traceLog.SubscriptionId ?? (object)DBNull.Value);
                pgcom.Parameters.AddWithValue("responsecode", traceLog.ResponseCode ?? (object)DBNull.Value);
                pgcom.Parameters.AddWithValue("subscriberendpoint", traceLog.SubscriberEndpoint ?? (object)DBNull.Value);

                await pgcom.ExecuteNonQueryAsync();
            }
            catch (Exception e)
            {
                _logger.LogError("Error while creating trace log entry {Message}", e.Message);
            }
        }
    }
}
