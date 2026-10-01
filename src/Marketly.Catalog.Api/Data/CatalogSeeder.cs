using Marketly.Catalog.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Marketly.Catalog.Api.Data;

public static class CatalogSeeder
{
    /// <summary>Inserts deterministic sample catalog rows once, leaving existing catalog data untouched.</summary>
    public static async Task SeedAsync(CatalogDbContext db, CancellationToken ct = default)
    {
        if (await db.Products.AnyAsync(ct)) return;
        db.Products.AddRange(
            Product("HOME-001", "Everyday Stoneware Set", "A considered set of four hand-finished stoneware plates.", "Home", 48, 80, "photo-1610701596007-11502861dcfa"),
            Product("HOME-002", "The Sunday Throw", "Soft recycled cotton for slow mornings and late nights.", "Home", 72, 42, "photo-1600210492486-724fe5c67fb0"),
            Product("TECH-001", "Pocket Speaker Mini", "Big, balanced sound in a take-anywhere speaker.", "Tech", 39, 120, "photo-1608043152269-423dbba4e7e1"),
            Product("STYLE-001", "Canvas Market Tote", "A sturdy everyday carryall made from durable cotton canvas.", "Style", 28, 95, "photo-1544816155-12df9643f363"),
            Product("KITCHEN-001", "Ribbed Glass Carafe", "A ribbed recycled-glass carafe for the everyday table.", "Kitchen", 34, 64, "photo-1514228742587-6b1558fcca3d"),
            Product("HOME-003", "Sunday Morning Candle", "A clean-burning soy candle with cedar and bergamot notes.", "Home", 26, 130, "photo-1602874801007-bd458bb1b8b6"),
            Product("TECH-002", "Studio Wireless Headphones", "Comfortable wireless listening with up to 30 hours of battery.", "Tech", 89, 38, "photo-1505740420928-5e560c06d30e"),
            Product("STYLE-002", "Weekend Crossbody", "A compact recycled-leather bag with room for the essentials.", "Style", 56, 51, "photo-1548036328-c9fa89d128fa"));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Builds a product row and attaches its stable demo image URL.</summary>
    private static CatalogProduct Product(string sku, string name, string description, string category, decimal price, int inventory, string image) =>
        new() { Id = StableProductId(sku), Sku = sku, Name = name, Description = description, Category = category, Price = price, Inventory = inventory,
            ImageUrl = $"https://images.unsplash.com/{image}" };

    /// <summary>Maps demo SKUs to fixed IDs so documentation and local API examples remain repeatable.</summary>
    private static Guid StableProductId(string sku) => sku switch
    {
        "HOME-001" => Guid.Parse("00000000-0000-0000-0000-000000000001"),
        "HOME-002" => Guid.Parse("00000000-0000-0000-0000-000000000002"),
        "TECH-001" => Guid.Parse("00000000-0000-0000-0000-000000000003"),
        "STYLE-001" => Guid.Parse("00000000-0000-0000-0000-000000000004"),
        "KITCHEN-001" => Guid.Parse("00000000-0000-0000-0000-000000000005"),
        "HOME-003" => Guid.Parse("00000000-0000-0000-0000-000000000006"),
        "TECH-002" => Guid.Parse("00000000-0000-0000-0000-000000000007"),
        "STYLE-002" => Guid.Parse("00000000-0000-0000-0000-000000000008"),
        _ => throw new ArgumentOutOfRangeException(nameof(sku), sku, "Demo SKU is not mapped to a stable identifier.")
    };
}
