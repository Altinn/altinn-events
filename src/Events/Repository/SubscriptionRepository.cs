using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Extensions;
using Altinn.Platform.Events.Models;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Platform.Events.Repository;

/// <summary>
/// Represents an implementation of <see cref="ISubscriptionRepository"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly string _findSubscriptionSql = "select * from events.find_subscription(@resourcefilter, @sourcefilter, @subjectfilter, @typefilter, @consumer, @endpointurl)";
    private readonly string _insertSubscriptionSql = "select * from events.insert_subscription(@resourcefilter, @sourcefilter, @subjectfilter, @typefilter, @consumer, @endpointurl, @createdby, @validated, @sourcefilterhash)";
    private readonly string _getSubscriptionSql = "select * from events.getsubscription_v2(@_id)";
    private readonly string _deleteSubscription = "call events.deletesubscription(@_id)";
    private readonly string _setValidSubscription = "call events.setvalidsubscription(@_id)";
    private readonly string _getSubscriptionsSql = "select * from events.getsubscriptions($1, $2, $3)";
    private readonly string _getSubscriptionByConsumerSql = "select * from events.getsubscriptionsbyconsumer_v2(@_consumer, @_includeInvalid)";

    private readonly NpgsqlDataSource _dataSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionRepository"/> class with the given dependencies.
    /// </summary>
    public SubscriptionRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc/>
    public async Task<Subscription> CreateSubscription(Subscription eventsSubscription, string sourceFilterHash)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertSubscriptionSql);
        pgcom.Parameters.AddWithValue("resourcefilter", eventsSubscription.ResourceFilter.ToLower());

        pgcom.Parameters.AddWithNullableString("sourcefilter", eventsSubscription.SourceFilter?.AbsoluteUri);
        pgcom.Parameters.AddWithNullableString("subjectfilter", eventsSubscription.SubjectFilter);
        pgcom.Parameters.AddWithNullableString("typefilter", eventsSubscription.TypeFilter);

        pgcom.Parameters.AddWithValue("consumer", eventsSubscription.Consumer);
        pgcom.Parameters.AddWithValue("endpointurl", eventsSubscription.EndPoint.AbsoluteUri);
        pgcom.Parameters.AddWithValue("createdby", eventsSubscription.CreatedBy);
        pgcom.Parameters.AddWithValue("validated", false);
        pgcom.Parameters.AddWithNullableString("sourcefilterhash", sourceFilterHash);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        await reader.ReadAsync();

        return GetSubscription(reader);
    }

    /// <inheritdoc/>
    public async Task<Subscription> FindSubscription(
        Subscription eventsSubscription, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_findSubscriptionSql);

        pgcom.Parameters.AddWithValue("resourcefilter", eventsSubscription.ResourceFilter.ToLower());

        pgcom.Parameters.AddWithNullableString("sourcefilter", eventsSubscription.SourceFilter?.AbsoluteUri);
        pgcom.Parameters.AddWithNullableString("subjectfilter", eventsSubscription.SubjectFilter);
        pgcom.Parameters.AddWithNullableString("typefilter", eventsSubscription.TypeFilter);

        pgcom.Parameters.AddWithValue("consumer", eventsSubscription.Consumer);
        pgcom.Parameters.AddWithValue("endpointurl", eventsSubscription.EndPoint.AbsoluteUri);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            return GetSubscription(reader);
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<List<Subscription>> GetSubscriptions(
        string resource, string subject, string eventType, CancellationToken cancellationToken)
    {
        List<Subscription> searchResult = [];

        await using NpgsqlCommand command = _dataSource.CreateCommand(_getSubscriptionsSql);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, resource);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, subject ?? string.Empty);
        command.Parameters.AddWithValue(NpgsqlDbType.Varchar, eventType);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            searchResult.Add(GetSubscription(reader));
        }

        return searchResult;
    }

    /// <inheritdoc/>
    public async Task DeleteSubscription(int id)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_deleteSubscription);
        pgcom.Parameters.AddWithValue("_id", id);

        await pgcom.ExecuteNonQueryAsync();
    }

    /// <inheritdoc/>
    public async Task SetValidSubscription(int id)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_setValidSubscription);
        pgcom.Parameters.AddWithValue("_id", id);

        await pgcom.ExecuteNonQueryAsync();
    }

    /// <inheritdoc/>
    public async Task<Subscription> GetSubscription(int id)
    {
        Subscription subscription = null;

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getSubscriptionSql);
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
        List<Subscription> searchResult = new();

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getSubscriptionByConsumerSql);
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
        var sourceFilter = reader.GetValue<string>("sourcefilter");

        Subscription subscription = new()
        {
            Id = reader.GetValue<int>("id"),
            ResourceFilter = reader.GetValue<string>("resourcefilter"),
            SourceFilter = sourceFilter != null ? new Uri(sourceFilter) : null,
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
