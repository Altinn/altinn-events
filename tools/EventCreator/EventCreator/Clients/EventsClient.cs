using Altinn.Platform.Storage.Interface.Models;

using Npgsql;
using NpgsqlTypes;

namespace EventCreator.Clients;

public record AppInstanceEvent(
    DateTime RegisteredTime,
    string EventId,
    string EventType);

public class EventsClient
{
    private const string _getInstanceEventsSql = """
        SELECT registeredtime, cloudevent->>'id' AS eventid, cloudevent->>'type' AS eventtype
        FROM events.events
        WHERE registeredtime > $1
        AND cloudevent->>'resource' = $2
        AND cloudevent->>'resourceinstance' = $3
        ORDER BY registeredtime
        LIMIT 100
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly int _commandTimeoutSeconds;

    public EventsClient(string connectionString, int commandTimeoutSeconds)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        _dataSource = dataSourceBuilder.Build();
        _commandTimeoutSeconds = commandTimeoutSeconds;
    }

    public async Task<List<AppInstanceEvent>> GetInstanceEvents(Instance instance)
    {
        string[] appIdParts = instance.AppId.Split('/');
        string resource = $"urn:altinn:resource:app_{appIdParts[0]}_{appIdParts[1]}";
        string resourceInstance = $"{instance.InstanceOwner.PartyId}/{instance.Id}";

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getInstanceEventsSql);
        pgcom.CommandTimeout = _commandTimeoutSeconds;
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, instance.Created!.Value.AddHours(-1));
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, resource);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, resourceInstance);

        List<AppInstanceEvent> events = [];
        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            events.Add(new AppInstanceEvent(
                RegisteredTime: reader.GetDateTime(0),
                EventId: reader.GetString(1),
                EventType: reader.GetString(2)));
        }

        return events;
    }
}
