using Marketly.Catalog.Api.Data.Entities;

namespace Marketly.Catalog.Api.DTOs;

/// <summary>Bounded repository result for a Catalog search.</summary>
public sealed record ProductPage(IReadOnlyList<CatalogProduct> Items, int Page, int PageSize, int Total);
