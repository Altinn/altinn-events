#nullable enable
using System.Text.Json;
using System.Threading.Tasks;
using Npgsql;

namespace Altinn.Platform.Events.IntegrationTests.Utils;

/// <summary>
/// Utility methods for working with PostgreSQL in integration tests.
/// </summary>
public static class PostgresTestUtils
{
    /// <summary>
    /// Retrieves an event from the database by its ID.
    /// Retries multiple times to allow for transaction commit and visibility.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <param name="eventId">The ID of the event to retrieve.</param>
    /// <param name="maxAttempts">Maximum number of attempts before giving up. Defaults to 20.</param>
    /// <param name="delayMs">Delay in milliseconds between attempts. Defaults to 100ms.</param>
    /// <returns>The event as a JsonDocument, or null if not found.</returns>
    public static async Task<JsonDocument?> GetEventFromDatabaseAsync(
        string connectionString,
        string eventId,
        int maxAttempts = 20,
        int delayMs = 100)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        return await WaitForUtils.WaitForAsync(
            async () =>
            {
                await using var command = dataSource.CreateCommand(
                    "SELECT cloudevent FROM events.events WHERE cloudevent->>'id' = $1");
                command.Parameters.AddWithValue(eventId);

                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var json = reader.GetString(0);
                    return JsonDocument.Parse(json);
                }

                return null;
            },
            maxAttempts,
            delayMs);
    }
}
