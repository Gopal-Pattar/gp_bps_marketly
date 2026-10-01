namespace Marketly.Catalog.Api.Data.Entities;

/// <summary>Tracks stock held for a checkout until it is committed or released.</summary>
public sealed class InventoryReservation
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string State { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
