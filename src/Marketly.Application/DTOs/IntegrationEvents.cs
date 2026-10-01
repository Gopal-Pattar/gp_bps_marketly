namespace Marketly.Application.DTOs;

/// <summary>Integration event persisted in the Orders outbox after a completed checkout.</summary>
public sealed record OrderPlaced(Guid OrderId, Guid CustomerId, decimal Total, DateTime CreatedAt);
