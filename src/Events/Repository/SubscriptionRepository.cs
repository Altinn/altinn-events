using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Platform.Events.Repository
{
    /// <summary>
    /// Represents an implementation of <see cref="ISubscriptionRepository"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly string findSubscriptionSql = "select * from events.find_subscription(@sourcefilter, @subjectfilter, @typefilter, @consumer, @endpointurl)";
        private readonly string insertSubscriptionSql = "select * from events.insert_subscription(@sourcefilter, @subjectfilter, @typefilter, @consumer, @endpointurl, @createdby, @validated, @sourcefilterhash)";
        private readonly string getSubscriptionSql = "select * from events.getsubscription(@_id)";
        private readonly string deleteSubscription = "call events.deletesubscription(@_id)";
        private readonly string setValidSubscription = "call events.setvalidsubscription(@_id)";
        private readonly string getSubscriptionsSql = "select * from events.getsubscriptions($1, $2, $3)";
        private readonly string getSubscriptionByConsumerSql = "select * from events.getsubscriptionsbyconsumer(@_consumer, @_includeInvalid)";
        private readonly string connectionString;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionRepository"/> class with a 
        /// PostgreSQL connection string.
        /// </summary>
        public SubscriptionRepository(IOptions<PostgreSqlSettings> postgresSettings)
        {
            connectionString =
                string.Format(postgresSettings.Value.ConnectionString, postgresSettings.Value.EventsDbPwd);
        }

        /// <inheritdoc/>
        public async Task<Subscription> CreateSubscription(Subscription eventsSubscription, string sourceFilterHash)
        {
            await using NpgsqlConnection conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            await using NpgsqlCommand pgcom = new NpgsqlCommand(insertSubscriptionSql, conn);
            pgcom.Parameters.AddWithValue("sourcefilter", eventsSubscription.SourceFilter.AbsoluteUri);

            pgcom.Parameters.AddWithNullableString("subjectfilter", eventsSubscription.SubjectFilter);
            pgcom.Parameters.AddWithNullableString("typefilter", eventsSubscription.TypeFilter);

            pgcom.Parameters.AddWithValue("consumer", eventsSubscription.Consumer);
            pgcom.Parameters.AddWithValue("endpointurl", eventsSubscription.EndPoint.AbsoluteUri);
            pgcom.Parameters.AddWithValue("createdby", eventsSubscription.CreatedBy);
            pgcom.Parameters.AddWithValue("validated", false);
            pgcom.Parameters.AddWithValue("sourcefilterhash", sourceFilterHash);

            await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
            await reader.ReadAsync();

            return GetSubscription(reader);
        }

        /// <inheritdoc/>
        public async Task<Subscription> FindSubscription(Subscription eventsSubscription, CancellationToken ct)
        {
            await using NpgsqlConnection conn = new(connectionString);
            await conn.OpenAsync(ct);

            await using NpgsqlCommand pgcom = new(findSubscriptionSql, conn);

            pgcom.Parameters.AddWithValue("sourcefilter", eventsSubscription.SourceFilter.AbsoluteUri);

            pgcom.Parameters.AddWithNullableString("subjectfilter", eventsSubscription.SubjectFilter);
            pgcom.Parameters.AddWithNullableString("typefilter", eventsSubscription.TypeFilter);

            pgcom.Parameters.AddWithValue("consumer", eventsSubscription.Consumer);
            pgcom.Parameters.AddWithValue("endpointurl", eventsSubscription.EndPoint.AbsoluteUri);

            await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(ct);

            if (await reader.ReadAsync(ct))
            {
                return GetSubscription(reader);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<List<Subscription>> GetSubscriptions(List<string> sourceFilterHashes, string source, string subject, string type, CancellationToken ct)
        {
            List<Subscription> searchResult = new();

            await using NpgsqlConnection conn = new(connectionString);
            await conn.OpenAsync(ct);
            await using NpgsqlCommand pgcom = new(getSubscriptionsSql, conn)
            {
                Parameters =
                {
                    new() { Value = sourceFilterHashes },
                    new() { Value = subject ?? string.Empty, NpgsqlDbType = NpgsqlDbType.Varchar, IsNullable = false },
                    new() { Value = type }
                }
            };

            await pgcom.PrepareAsync(ct);
            await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    searchResult.Add(GetSubscription(reader));
                }
            }

            return searchResult;
        }

        /// <inheritdoc/>
        public async Task DeleteSubscription(int id)
        {
            await using NpgsqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            await using NpgsqlCommand pgcom = new(deleteSubscription, conn);
            pgcom.Parameters.AddWithValue("_id", id);

            await pgcom.ExecuteNonQueryAsync();
        }

        /// <inheritdoc/>
        public async Task SetValidSubscription(int id)
        {
            await using NpgsqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            await using NpgsqlCommand pgcom = new NpgsqlCommand(setValidSubscription, conn);
            pgcom.Parameters.AddWithValue("_id", id);

            await pgcom.ExecuteNonQueryAsync();
        }

        /// <inheritdoc/>
        public async Task<Subscription> GetSubscription(int id)
        {
            Subscription subscription = null;

            await using NpgsqlConnection conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            await using NpgsqlCommand pgcom = new NpgsqlCommand(getSubscriptionSql, conn);
            pgcom.Parameters.AddWithValue("_id", NpgsqlDbType.Integer, id);

            await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    subscription = GetSubscription(reader);
                }
            }

            return subscription;
        }

        /// <inheritdoc/>
        public async Task<List<Subscription>> GetSubscriptionsByConsumer(string consumer, bool includeInvalid)
        {
            List<Subscription> searchResult = new List<Subscription>();

            await using NpgsqlConnection conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            await using NpgsqlCommand pgcom = new NpgsqlCommand(getSubscriptionByConsumerSql, conn);
            pgcom.Parameters.AddWithValue("_consumer", NpgsqlDbType.Varchar, consumer);
            pgcom.Parameters.AddWithValue("_includeInvalid", NpgsqlDbType.Boolean, includeInvalid);
            await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    searchResult.Add(GetSubscription(reader));
                }
            }

            return searchResult;
        }

        private static Subscription GetSubscription(NpgsqlDataReader reader)
        {
            Subscription subscription = new Subscription
            {
                Id = reader.GetValue<int>("id"),
                SourceFilter = new Uri(reader.GetValue<string>("sourcefilter")),
                SubjectFilter = reader.GetValue<string>("subjectfilter"),
                TypeFilter = reader.GetValue<string>("typefilter"),
                Consumer = reader.GetValue<string>("consumer"),
                EndPoint = new Uri(reader.GetValue<string>("endpointurl")),
                CreatedBy = reader.GetValue<string>("createdby"),
                Created = reader.GetValue<DateTime>("time").ToUniversalTime(),
                Validated = reader.GetValue<bool>("validated")
            };
            return subscription;
        }
    }
}
