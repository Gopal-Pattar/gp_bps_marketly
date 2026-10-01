namespace Marketly.Domain;

public sealed record Product
{
    public Guid Id { get; init; }
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public decimal Price { get; init; }
    public int Inventory { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsActive { get; init; }
}

