namespace Marketly.Catalog.Api.DTOs;

/// <summary>Stock reservation request from Orders to Catalog.</summary>
public sealed record InventoryRequest(Guid ReservationId, Guid ProductId, int Quantity);

/// <summary>Set of reservation IDs to commit together.</summary>
public sealed record ReservationCommitRequest(List<Guid> ReservationIds);
