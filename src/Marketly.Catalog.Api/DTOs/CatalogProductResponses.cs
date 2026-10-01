namespace Marketly.Catalog.Api.DTOs;

/// <summary>Public product data returned to storefront clients.</summary>
public sealed record CatalogProductResponse(
    Guid Id, string Sku, string Name, string Description, string Category,
    decimal Price, int Inventory, string? ImageUrl);

/// <summary>Public paged search result; persistence entities never cross the API boundary.</summary>
public sealed record CatalogProductPageResponse(
    IReadOnlyList<CatalogProductResponse> Items, int Page, int PageSize, int Total);
