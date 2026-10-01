namespace Marketly.Application.DTOs;

public sealed record OrderLineSummary(Guid ProductId, int Quantity, decimal UnitPrice);
public sealed record ShippingAddress(string Name, string AddressLine1, string AddressLine2, string City, string Region, string PostalCode, string CountryCode);
public sealed record OrderSummary(Guid Id, DateTime CreatedAt, string Status, decimal Subtotal, decimal Shipping, decimal Tax, decimal Total, ShippingAddress ShippingAddress, IReadOnlyList<OrderLineSummary> Items);
public sealed record OrderReceipt(Guid Id, string Status, string PaymentReference, decimal Subtotal, decimal Shipping, decimal Tax, decimal Total, DateTime CreatedAt);
