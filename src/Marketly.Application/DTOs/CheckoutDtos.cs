namespace Marketly.Application.DTOs;

/// <summary>One product and quantity requested for checkout.</summary>
public sealed record CheckoutLine(Guid ProductId, int Quantity);

/// <summary>Delivery details captured with an order.</summary>
public sealed record CheckoutAddress(string Name, string AddressLine1, string? AddressLine2, string City, string Region, string PostalCode, string CountryCode);

/// <summary>Normalized checkout request persisted so an interrupted saga can resume.</summary>
public sealed record CheckoutCommand(IReadOnlyList<CheckoutLine> Items, CheckoutAddress DeliveryAddress);

/// <summary>Result returned by checkout use cases.</summary>
public sealed record CheckoutResult(OrderReceipt? Order, string? Error, bool Pending = false, Guid? SagaId = null, PaymentIntent? Payment = null)
{
    public bool Succeeded => Order is not null;
}

/// <summary>Safe client-facing payment details; never contains the provider secret.</summary>
public sealed record PaymentIntent(string Provider, string ProviderOrderId, string PublicKeyId, long AmountSubunits, string Currency);
