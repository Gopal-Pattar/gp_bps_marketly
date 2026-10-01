using Marketly.Catalog.Api.DTOs;
using Marketly.Catalog.Api.Data;
using Marketly.Catalog.Api.Data.Entities;
using Marketly.Catalog.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Marketly.Catalog.Api.Repositories;

public sealed class CatalogRepository(CatalogDbContext db) : ICatalogRepository
{
    /// <summary>Applies validated paging and optional text/category/price filters to the catalog query.</summary>
    public async Task<ProductPage> SearchAsync(string? search, string? category, string? sort, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Products.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => EF.Functions.ILike(x.Name, $"%{search.Trim()}%") || EF.Functions.ILike(x.Description, $"%{search.Trim()}%"));
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category.ToLower() == category.Trim().ToLower());
        query = sort?.ToLowerInvariant() switch
        {
            "price-asc" => query.OrderBy(x => x.Price),
            "price-desc" => query.OrderByDescending(x => x.Price),
            "name" => query.OrderBy(x => x.Name),
            _ => query.OrderBy(x => x.Name)
        };
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new ProductPage(items, page, pageSize, total);
    }

    /// <summary>Reads an active product without tracking it for later updates.</summary>
    public Task<CatalogProduct?> GetAsync(Guid id, CancellationToken ct) =>
        db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

    /// <summary>
    /// Atomically subtracts stock and stores an expiring reservation. If the same reservation
    /// ID is retried, it validates and reuses the existing reservation instead of decrementing twice.
    /// </summary>
    public async Task<bool> ReserveStockAsync(Guid reservationId, Guid productId, int quantity, CancellationToken ct)
    {
        var existing = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == reservationId, ct);
        if (existing is not null)
            return IsReusable(existing, productId, quantity);

        var reservation = new InventoryReservation
        {
            Id = reservationId, ProductId = productId, Quantity = quantity,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var reserved = await db.Products.Where(p => p.Id == productId && p.IsActive && p.Inventory >= quantity)
                .ExecuteUpdateAsync(update => update.SetProperty(p => p.Inventory, p => p.Inventory - quantity), ct);
            if (reserved != 1) return false;
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Concurrent delivery of the same reservation ID may race before either row exists.
            // The unique reservation key rolls back this transaction and therefore its stock decrement.
            db.Entry(reservation).State = EntityState.Detached;
            var concurrent = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == reservationId, ct);
            if (concurrent is not null) return IsReusable(concurrent, productId, quantity);
            throw;
        }
    }

    /// <summary>Checks whether an existing reservation matches and can safely be reused.</summary>
    private static bool IsReusable(InventoryReservation reservation, Guid productId, int quantity) =>
        reservation.ProductId == productId && reservation.Quantity == quantity &&
        (reservation.State is "Active" or "Committed") &&
        (reservation.State == "Committed" || reservation.ExpiresAt > DateTime.UtcNow);

    /// <summary>Marks a reservation released and restores its units under a row lock.</summary>
    public async Task<bool> ReleaseStockAsync(Guid reservationId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reservation = await db.Reservations.FromSqlInterpolated($"SELECT * FROM \"Reservations\" WHERE \"Id\" = {reservationId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (reservation is null || reservation.State is "Released" or "Expired")
        {
            await tx.CommitAsync(ct);
            return true;
        }
        reservation.State = "Released";
        await db.Products.Where(x => x.Id == reservation.ProductId)
            .ExecuteUpdateAsync(update => update.SetProperty(p => p.Inventory, p => p.Inventory + reservation.Quantity), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>Locks and validates all reservations before committing any of them.</summary>
    public async Task<bool> CommitReservationsAsync(IReadOnlyCollection<Guid> reservationIds, CancellationToken ct)
    {
        var ids = reservationIds.Distinct().ToArray();
        if (ids.Length == 0) return false;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var found = await db.Reservations.FromSqlInterpolated($"SELECT * FROM \"Reservations\" WHERE \"Id\" = ANY ({ids}) ORDER BY \"Id\" FOR UPDATE")
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        if (found.Count != ids.Length || found.Any(x => x.State is "Released" or "Expired" || x.State == "Active" && x.ExpiresAt <= now))
            return false;
        foreach (var reservation in found)
            if (reservation.State == "Active") reservation.State = "Committed";
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
}

