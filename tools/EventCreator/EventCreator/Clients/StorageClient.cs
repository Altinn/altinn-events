using System.Data;

using Altinn.Platform.Storage.Interface.Models;

using Npgsql;
using NpgsqlTypes;

namespace EventCreator.Clients;

public class StorageClient
{
    private readonly string _readSqlNoElements = "select * from storage.readinstancenoelements ($1)";

    private readonly string _readSimilarArchivedInstancesSql = """
        SELECT instance
        FROM storage.instances
        WHERE appid = $1
        AND alternateid <> $2
        AND instance->'Status'->>'IsArchived' = 'true'
        AND (instance->'Status'->>'Archived')::timestamptz <= $3
        ORDER BY lastchanged DESC
        LIMIT $4
        """;

    private readonly NpgsqlDataSource _dataSource;

    public StorageClient(string _pgConnectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_pgConnectionString);
        dataSourceBuilder.EnableDynamicJson();
        _dataSource = dataSourceBuilder.Build();
    }

    public async Task<Instance?> GetOne(Guid instanceGuid)
    {
        Instance? instance = null;

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_readSqlNoElements);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, instanceGuid);

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                instance = await reader.GetFieldValueAsync<Instance>("instance");
            }
        }

        return instance;
    }

    public async Task<List<Instance>> GetSimilarArchivedInstances(string appId, Guid excludeInstanceGuid, int limit, DateTime archivedBefore)
    {
        List<Instance> instances = [];

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_readSimilarArchivedInstancesSql);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, appId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, excludeInstanceGuid);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, archivedBefore);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, limit);

        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            instances.Add(await reader.GetFieldValueAsync<Instance>("instance"));
        }

        return instances;
    }
}
