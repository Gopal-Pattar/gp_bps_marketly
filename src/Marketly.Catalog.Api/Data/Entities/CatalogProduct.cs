namespace Marketly.Catalog.Api.Data.Entities;

/// <summary>Catalog-owned product row.</summary>
public sealed class CatalogProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public decimal Price { get; set; }
    public int Inventory { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}
