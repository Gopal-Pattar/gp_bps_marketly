using Marketly.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Marketly.Infrastructure.Data;

public sealed class EfUnitOfWork(MarketlyDbContext db) : IUnitOfWork
{
    /// <summary>Persists all tracked changes in the current database context.</summary>
    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    /// <summary>Starts a database transaction for atomic order, saga, and outbox persistence.</summary>
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct) =>
        new EfUnitOfWorkTransaction(await db.Database.BeginTransactionAsync(ct));
}

internal sealed class EfUnitOfWorkTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
{
    /// <summary>Commits the transaction so all its changes become visible together.</summary>
    public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
    /// <summary>Disposes the transaction; an uncommitted transaction is rolled back.</summary>
    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
