using System;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Models;

using Microsoft.Extensions.Options;
using Npgsql;

namespace Altinn.Platform.Events.Repository
{
    /// <inheritdoc/>
    [ExcludeFromCodeCoverage]
    public class TraceLogRepository(NpgsqlDataSource dataSource) : ITraceLogRepository
    {
        private readonly string _insertTraceLogSql = @"INSERT INTO events.trace_log(
	cloudeventid, resource, eventtype, consumer, subscriptionid, responsecode, subscriberendpoint, activity)
	VALUES (@cloudeventid, @resource, @eventtype, @consumer, @subscriptionid, @responsecode, @subscriberendpoint, @activity);";

        private readonly NpgsqlDataSource _dataSource = dataSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceLog"/> class with a
        /// </summary>
        /// <param name="traceLog">Entity model to be stored to persistence</param>
        /// <returns></returns>
        public async Task CreateTraceLogEntry(TraceLog traceLog)
        {
            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertTraceLogSql);

            pgcom.Parameters.AddWithValue("activity", traceLog.Activity.ToString());

            // nullable values
            pgcom.Parameters.AddWithValue("cloudeventid", traceLog.CloudEventId ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue("eventtype", traceLog.EventType ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue("resource", traceLog.Resource ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue("consumer", traceLog.Consumer ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue("subscriptionid", traceLog.SubscriptionId ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue("responsecode", traceLog.ResponseCode ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue("subscriberendpoint", traceLog.SubscriberEndpoint ?? (object)DBNull.Value);

            await pgcom.ExecuteNonQueryAsync();
        }
    }
}
