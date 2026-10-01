using Marketly.Application.Interfaces;
using Marketly.Domain;
using Marketly.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Marketly.Infrastructure.Repositories;

public sealed class OrderRepository(MarketlyDbContext db) : IOrderRepository
{
    /// <summary>Tracks a new order; the unit of work persists it with saga and outbox changes.</summary>
    public void Add(Order order) => db.Orders.Add(order);

    /// <summary>Reads the customer's order history and includes its purchased lines.</summary>
    public async Task<IReadOnlyList<Order>> ForCustomerAsync(Guid customerId, CancellationToken ct) =>
        await db.Orders.AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(ct);
}

public sealed class CheckoutSagaRepository(MarketlyDbContext db) : ICheckoutSagaRepository
{
    /// <summary>Loads a saga by its stable checkout identifier.</summary>
    public Task<CheckoutSaga?> GetAsync(Guid id, CancellationToken ct) =>
        db.CheckoutSagas.SingleOrDefaultAsync(x => x.Id == id, ct);

    /// <summary>Finds the previous attempt for a customer's idempotency key.</summary>
    public Task<CheckoutSaga?> FindByKeyAsync(Guid customerId, string idempotencyKey, CancellationToken ct) =>
        db.CheckoutSagas.SingleOrDefaultAsync(x => x.CustomerId == customerId && x.IdempotencyKey == idempotencyKey, ct);

    /// <summary>
    /// Reuses the existing saga or inserts a new one. The unique customer/key index resolves
    /// races when two API requests attempt the same checkout at once.
    /// </summary>
    public async Task<CheckoutSaga> GetOrCreateAsync(Guid customerId, string idempotencyKey, string requestHash, string requestJson, CancellationToken ct)
    {
        var existing = await FindByKeyAsync(customerId, idempotencyKey, ct);
        if (existing is not null) return existing;
        var saga = new CheckoutSaga { CustomerId = customerId, IdempotencyKey = idempotencyKey, RequestHash = requestHash, RequestJson = requestJson };
        db.CheckoutSagas.Add(saga);
        try
        {
            await db.SaveChangesAsync(ct);
            return saga;
        }
        catch (DbUpdateException)
        {
            db.Entry(saga).State = EntityState.Detached;
            var concurrent = await FindByKeyAsync(customerId, idempotencyKey, ct);
            if (concurrent is not null) return concurrent;
            throw;
        }
    }

    /// <summary>Gets due nonterminal saga IDs for the background recovery worker.</summary>
    public async Task<IReadOnlyList<Guid>> GetRecoverableIdsAsync(CancellationToken ct) =>
        await db.CheckoutSagas.AsNoTracking()
            .Where(x => x.State != "Completed" && x.State != "Failed" && (x.RetryAfter == null || x.RetryAfter <= DateTime.UtcNow))
            .OrderBy(x => x.UpdatedAt).Select(x => x.Id).Take(50).ToListAsync(ct);

    /// <summary>
    /// Tries to acquire a PostgreSQL session advisory lock so only one API instance or
    /// recovery worker advances a saga at a time.
    /// </summary>
    public async Task<ICheckoutSagaLease?> TryAcquireLeaseAsync(Guid id, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(hashtextextended(@key, 0))";
        var key = command.CreateParameter();
        key.ParameterName = "key";
        key.Value = id.ToString("N");
        command.Parameters.Add(key);
        if ((bool)(await command.ExecuteScalarAsync(ct) ?? false)) return new PostgresSagaLease(db, id);
        await db.Database.CloseConnectionAsync();
        return null;
    }

    private sealed class PostgresSagaLease(MarketlyDbContext db, Guid id) : ICheckoutSagaLease
    {
        /// <summary>Releases the saga's session lock and returns its connection to the pool.</summary>
        public async ValueTask DisposeAsync()
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT pg_advisory_unlock(hashtextextended(@key, 0))";
            var key = command.CreateParameter();
            key.ParameterName = "key";
            key.Value = id.ToString("N");
            command.Parameters.Add(key);
            await command.ExecuteScalarAsync();
            await db.Database.CloseConnectionAsync();
        }
    }
}
