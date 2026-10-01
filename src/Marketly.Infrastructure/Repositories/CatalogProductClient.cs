using System.Net.Http.Json;
using Marketly.Application.Interfaces;
using Marketly.Domain;
using Microsoft.Extensions.Configuration;

namespace Marketly.Infrastructure.Repositories;

public sealed class CatalogProductClient(HttpClient http, IConfiguration configuration) : IProductRepository
{
    private readonly string serviceKey = configuration["ServiceAuth:ApiKey"]
        ?? throw new InvalidOperationException("ServiceAuth:ApiKey is required for Catalog inventory operations.");
    /// <summary>Fetches one active product from Catalog over its service API.</summary>
    public async Task<Product?> GetAsync(Guid id, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/api/products/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<Product>(cancellationToken: ct);
        return item is { IsActive: true } ? item : null;
    }

    /// <summary>Loads all requested active products and indexes them by ID for checkout pricing.</summary>
    public async Task<IReadOnlyDictionary<Guid, Product>> GetActiveByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var products = new Dictionary<Guid, Product>();
        foreach (var id in ids.Distinct())
            if (await GetAsync(id, ct) is { } product) products.Add(id, product);
        return products;
    }

    /// <summary>Calls Catalog's authenticated internal API to reserve stock for this saga.</summary>
    public async Task<bool> ReserveStockAsync(Guid reservationId, Guid productId, int quantity, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/inventory/reservations")
        {
            Content = JsonContent.Create(new InventoryRequest(reservationId, productId, quantity))
        };
        request.Headers.Add("X-Service-Key", serviceKey);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>Asks Catalog to atomically commit every reservation in this checkout.</summary>
    public async Task<bool> CommitReservationsAsync(IReadOnlyCollection<Guid> reservationIds, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/inventory/reservations/commit")
        {
            Content = JsonContent.Create(new ReservationCommitRequest(reservationIds.ToArray()))
        };
        request.Headers.Add("X-Service-Key", serviceKey);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>Releases one reservation through Catalog's authenticated internal API.</summary>
    public async Task ReleaseStockAsync(Guid reservationId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/internal/inventory/reservations/{reservationId}/release");
        request.Headers.Add("X-Service-Key", serviceKey);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private sealed record InventoryRequest(Guid ReservationId, Guid ProductId, int Quantity);
    private sealed record ReservationCommitRequest(Guid[] ReservationIds);
}
