using Marketly.Catalog.Api.DTOs;
using Marketly.Catalog.Api.Data.Entities;

namespace Marketly.Catalog.Api.Interfaces;

/// <summary>Catalog read and inventory-reservation operations available to the API layer.</summary>
public interface ICatalogRepository
{
    /// <summary>Searches active products, applies filters and sorting, and returns one bounded page.</summary>
    Task<ProductPage> SearchAsync(string? search, string? category, string? sort, int page, int pageSize, CancellationToken ct);
    /// <summary>Gets one active product by its identifier.</summary>
    Task<CatalogProduct?> GetAsync(Guid id, CancellationToken ct);
    /// <summary>Reserves available quantity using a stable, retry-safe reservation identifier.</summary>
    Task<bool> ReserveStockAsync(Guid reservationId, Guid productId, int quantity, CancellationToken ct);
    /// <summary>Commits a reservation set as a single Catalog database transaction.</summary>
    Task<bool> CommitReservationsAsync(IReadOnlyCollection<Guid> reservationIds, CancellationToken ct);
    /// <summary>Returns reserved units to stock; repeated release calls are safe.</summary>
    Task<bool> ReleaseStockAsync(Guid reservationId, CancellationToken ct);
}
