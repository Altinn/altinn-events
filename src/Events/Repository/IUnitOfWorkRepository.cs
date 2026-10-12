using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Repository;

/// <summary>
/// Describes operations for starting, committing, and rolling back a <see cref="UnitOfWork"/>,
/// including support for savepoints so that a partial rollback can be performed without
/// releasing locks held by the enclosing transaction (e.g. a claimed event row).
/// </summary>
public interface IUnitOfWorkRepository
{
    /// <summary>
    /// Opens a new connection and begins a transaction, returning a <see cref="UnitOfWork"/>
    /// wrapping both.
    /// </summary>
    Task<UnitOfWork> StartUnitOfWork();

    /// <summary>
    /// Commits the transaction and closes the connection associated with <paramref name="unitOfWork"/>.
    /// </summary>
    Task CommitUnitOfWork(UnitOfWork unitOfWork);

    /// <summary>
    /// Rolls back the transaction and closes the connection associated with <paramref name="unitOfWork"/>.
    /// </summary>
    Task RollbackUnitOfWork(UnitOfWork unitOfWork);

    /// <summary>
    /// Creates a savepoint named <paramref name="savepoint"/> within the transaction associated
    /// with <paramref name="unitOfWork"/>, allowing a later partial rollback to that point.
    /// </summary>
    Task SaveUnitOfWork(UnitOfWork unitOfWork, string savepoint);

    /// <summary>
    /// Rolls back the transaction associated with <paramref name="unitOfWork"/> to the given
    /// <paramref name="savepoint"/>, without committing or closing the connection, so that
    /// locks held earlier in the transaction (e.g. a claimed event row) remain in place.
    /// </summary>
    Task RollbackUnitOfWorkToSavepoint(UnitOfWork unitOfWork, string savepoint);
}
