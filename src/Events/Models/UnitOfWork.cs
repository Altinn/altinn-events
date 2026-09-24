using Npgsql;

namespace Altinn.Platform.Events.Models;

/// <summary>
/// Holds a database connection and transaction shared across multiple repository
/// operations, allowing e.g. claiming an event and later marking it processed or
/// scheduling a retry to happen within a single transaction.
/// </summary>
public class UnitOfWork
{
    /// <summary>
    /// Gets the connection associated with this unit of work.
    /// </summary>
    public required NpgsqlConnection Connection { get; init; }

    /// <summary>
    /// Gets the transaction associated with this unit of work.
    /// </summary>
    public required NpgsqlTransaction Transaction { get; init; }
}
