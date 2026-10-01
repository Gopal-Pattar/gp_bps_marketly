using Marketly.Catalog.Api.Data;
using Marketly.Catalog.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Marketly.Catalog.Api.Data.Migrations;

[DbContext(typeof(CatalogDbContext))]
public sealed class CatalogDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder) => Configure(modelBuilder);

    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");
        modelBuilder.Entity<CatalogProduct>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedOnAdd().HasColumnType("uuid");
            entity.Property(x => x.Sku).IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            entity.Property(x => x.Name).IsRequired().HasMaxLength(160).HasColumnType("character varying(160)");
            entity.Property(x => x.Description).IsRequired().HasColumnType("text");
            entity.Property(x => x.Category).IsRequired().HasMaxLength(80).HasColumnType("character varying(80)");
            entity.Property(x => x.Price).HasPrecision(12, 2).HasColumnType("numeric(12,2)");
            entity.Property(x => x.Inventory).HasColumnType("integer");
            entity.Property(x => x.ImageUrl).HasColumnType("text");
            entity.Property(x => x.IsActive).HasColumnType("boolean");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Sku).IsUnique();
            entity.HasIndex(x => new { x.Category, x.IsActive });
            entity.ToTable("Products");
        });
        modelBuilder.Entity<InventoryReservation>(entity =>
        {
            entity.Property(x => x.Id).HasColumnType("uuid");
            entity.Property(x => x.ProductId).HasColumnType("uuid");
            entity.Property(x => x.Quantity).HasColumnType("integer");
            entity.Property(x => x.State).IsRequired().HasMaxLength(24).HasColumnType("character varying(24)");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.ExpiresAt).HasColumnType("timestamp with time zone");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ExpiresAt);
            entity.ToTable("Reservations");
        });
    }
}
