using Marketly.Domain;

namespace Marketly.Application.Interfaces;

public interface IProductRepository
{
    /// <summary>Gets one active catalog product by ID.</summary>
    Task<Product?> GetAsync(Guid id, CancellationToken ct);
    /// <summary>Loads active products for one checkout price calculation.</summary>
    Task<IReadOnlyDictionary<Guid, Product>> GetActiveByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    /// <summary>Reserves a product's stock under a stable reservation ID.</summary>
    Task<bool> ReserveStockAsync(Guid reservationId, Guid productId, int quantity, CancellationToken ct);
    /// <summary>Commits the complete reservation set or leaves it uncommitted.</summary>
    Task<bool> CommitReservationsAsync(IReadOnlyCollection<Guid> reservationIds, CancellationToken ct);
    /// <summary>Releases one reservation and makes its units available again.</summary>
    Task ReleaseStockAsync(Guid reservationId, CancellationToken ct);
}

public interface IOrderRepository
{
    /// <summary>Tracks an order for persistence by the unit of work.</summary>
    void Add(Order order);
    /// <summary>Loads a customer's order history.</summary>
    Task<IReadOnlyList<Order>> ForCustomerAsync(Guid customerId, CancellationToken ct);
}

public interface ICheckoutSagaRepository
{
    /// <summary>Loads a persisted saga by its ID.</summary>
    Task<CheckoutSaga?> GetAsync(Guid id, CancellationToken ct);
    /// <summary>Finds an existing saga for a customer's idempotency key.</summary>
    Task<CheckoutSaga?> FindByKeyAsync(Guid customerId, string idempotencyKey, CancellationToken ct);
    /// <summary>Returns the matching saga or persists a new idempotent checkout attempt.</summary>
    Task<CheckoutSaga> GetOrCreateAsync(Guid customerId, string idempotencyKey, string requestHash, string requestJson, CancellationToken ct);
    /// <summary>Lists due nonterminal saga IDs for background recovery.</summary>
    Task<IReadOnlyList<Guid>> GetRecoverableIdsAsync(CancellationToken ct);
    /// <summary>Tries to acquire an exclusive processing lease for one saga.</summary>
    Task<ICheckoutSagaLease?> TryAcquireLeaseAsync(Guid id, CancellationToken ct);
}

/// <summary>Releases an exclusive saga-processing lease when disposed.</summary>
public interface ICheckoutSagaLease : IAsyncDisposable { }

public interface IUnitOfWork
{
    /// <summary>Saves all tracked changes in the current business transaction.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct);
    /// <summary>Begins a transaction that groups related database writes atomically.</summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    /// <summary>Makes every write in this transaction durable.</summary>
    Task CommitAsync(CancellationToken ct);
}
