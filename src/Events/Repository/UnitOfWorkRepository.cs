using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Altinn.Platform.Events.Models;
using Npgsql;

namespace Altinn.Platform.Events.Repository;

/// <summary>
/// Repository for handling unit of work related operations.
/// </summary>
/// <param name="dataSource">The npgsql data source.</param>
[ExcludeFromCodeCoverage]
public class UnitOfWorkRepository(NpgsqlDataSource dataSource) : IUnitOfWorkRepository
{
    /// <inheritdoc/>
    public async Task<UnitOfWork> StartUnitOfWork()
    {
        var connection = await dataSource.OpenConnectionAsync();

        try
        {
            var transaction = await connection.BeginTransactionAsync();
            return new UnitOfWork { Connection = connection, Transaction = transaction };
        }
        catch
        {
            await connection.CloseAsync();
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task CommitUnitOfWork(UnitOfWork unitOfWork)
    {
        try
        {
            await unitOfWork.Transaction.CommitAsync();
        }
        finally
        {
            await unitOfWork.Connection.CloseAsync();
        }
    }

    /// <inheritdoc/>
    public async Task RollbackUnitOfWork(UnitOfWork unitOfWork)
    {
        try
        {
            await unitOfWork.Transaction.RollbackAsync();
        }
        finally
        {
            await unitOfWork.Connection.CloseAsync();
        }
    }

    /// <inheritdoc/>
    public async Task SaveUnitOfWork(UnitOfWork unitOfWork, string savepoint)
    {
        await unitOfWork.Transaction.SaveAsync(savepoint);
    }

    /// <inheritdoc/>
    public async Task RollbackUnitOfWorkToSavepoint(UnitOfWork unitOfWork, string savepoint)
    {
        await unitOfWork.Transaction.RollbackAsync(savepoint);
    }
}
