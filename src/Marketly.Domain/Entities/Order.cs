namespace Marketly.Domain;

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Paid";
    public string PaymentReference { get; set; } = "";
    public string ShippingName { get; set; } = "";
    public string ShippingAddressLine1 { get; set; } = "";
    public string ShippingAddressLine2 { get; set; } = "";
    public string ShippingCity { get; set; } = "";
    public string ShippingRegion { get; set; } = "";
    public string ShippingPostalCode { get; set; } = "";
    public string ShippingCountryCode { get; set; } = "IN";
    public decimal Subtotal { get; set; }
    public decimal Shipping { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}

