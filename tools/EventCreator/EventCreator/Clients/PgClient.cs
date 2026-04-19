using System.Data;

using Altinn.Platform.Storage.Interface.Models;

using Npgsql;
using NpgsqlTypes;

namespace EventCreator.Clients;

public class PgClient
{
    private readonly string _readSqlNoElements = "select * from storage.readinstancenoelements ($1)";

    private readonly NpgsqlDataSource _dataSource;

    public PgClient(string _pgConnectionString)
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
}
