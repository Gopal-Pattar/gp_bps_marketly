using Marketly.Catalog.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Marketly.Catalog.Api.Data;

/// <summary>EF Core context for data owned exclusively by the Catalog service.</summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<CatalogProduct> Products => Set<CatalogProduct>();
    public DbSet<InventoryReservation> Reservations => Set<InventoryReservation>();

    /// <summary>Configures product indexes and numeric/string column constraints.</summary>
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<CatalogProduct>().HasIndex(x => x.Sku).IsUnique();
        model.Entity<CatalogProduct>().HasIndex(x => new { x.Category, x.IsActive });
        model.Entity<CatalogProduct>().Property(x => x.Price).HasPrecision(12, 2);
        model.Entity<CatalogProduct>().Property(x => x.Sku).HasMaxLength(64);
        model.Entity<CatalogProduct>().Property(x => x.Name).HasMaxLength(160);
        model.Entity<CatalogProduct>().Property(x => x.Category).HasMaxLength(80);
        model.Entity<InventoryReservation>().HasIndex(x => x.ExpiresAt);
        model.Entity<InventoryReservation>().Property(x => x.State).HasMaxLength(24);
    }
}
