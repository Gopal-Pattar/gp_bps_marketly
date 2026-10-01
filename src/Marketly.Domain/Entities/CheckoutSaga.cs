namespace Marketly.Domain;

/// <summary>Durable Orders checkout state persisted across retries and process restarts.</summary>
public sealed class CheckoutSaga
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string RequestHash { get; set; }
    public required string RequestJson { get; set; }
    public string? OrderJson { get; set; }
    public string? PaymentProvider { get; set; }
    public string? PaymentProviderOrderId { get; set; }
    public string? PaymentPublicKeyId { get; set; }
    public long? PaymentAmountSubunits { get; set; }
    public string? PaymentCurrency { get; set; }
    public DateTime? PaymentExpiresAt { get; set; }
    public string State { get; set; } = "Started";
    public Guid? OrderId { get; set; }
    public string? ReceiptJson { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RetryAfter { get; set; }
}
