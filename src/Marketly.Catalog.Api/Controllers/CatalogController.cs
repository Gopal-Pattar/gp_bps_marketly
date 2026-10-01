using Microsoft.AspNetCore.Mvc;
using AutoMapper;
using Marketly.Catalog.Api.DTOs;
using Marketly.Catalog.Api.Data;
using Marketly.Catalog.Api.Data.Entities;
using Marketly.Catalog.Api.Interfaces;

namespace Marketly.Catalog.Api.Controllers;

/// <summary>Public product-browsing endpoints exposed by Catalog.</summary>
[ApiController]
[Route("api/products")]
public sealed class CatalogController(ICatalogRepository catalog, IMapper mapper) : ControllerBase
{
    /// <summary>Searches active products with optional search, category, sort, and paging filters.</summary>
    [HttpGet]
    public async Task<ActionResult<ProductPage>> Search(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken) =>
        Ok(mapper.Map<CatalogProductPageResponse>(
            await catalog.SearchAsync(search, category, sort, page ?? 1, pageSize ?? 24, cancellationToken)));

    /// <summary>Returns one active product or 404 when the ID is not available.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CatalogProduct>> Get(Guid id, CancellationToken cancellationToken)
    {
        var product = await catalog.GetAsync(id, cancellationToken);
        return product is null ? NotFound() : Ok(mapper.Map<CatalogProductResponse>(product));
    }
}

/// <summary>Internal inventory operations called by Orders using the service API key.</summary>
[ApiController]
[Route("internal/inventory/reservations")]
public sealed class InventoryReservationsController(ICatalogRepository catalog, IConfiguration configuration) : ControllerBase
{
    /// <summary>Reserves stock for an order saga; repeated reservation IDs do not subtract stock twice.</summary>
    [HttpPost]
    public async Task<IActionResult> Reserve([FromBody] InventoryRequest request, CancellationToken cancellationToken)
    {
        if (!IsAuthorized()) return Unauthorized();
        if (request.ReservationId == Guid.Empty || request.Quantity is < 1 or > 20) return BadRequest();
        var reserved = await catalog.ReserveStockAsync(request.ReservationId, request.ProductId, request.Quantity, cancellationToken);
        return reserved ? NoContent() : Conflict(new { message = "Product unavailable or inventory is insufficient." });
    }

    /// <summary>Atomically commits the full set of inventory reservations for a checkout.</summary>
    [HttpPost("commit")]
    public async Task<IActionResult> Commit([FromBody] ReservationCommitRequest request, CancellationToken cancellationToken)
    {
        if (!IsAuthorized()) return Unauthorized();
        if (request.ReservationIds is null || request.ReservationIds.Count is 0 or > 30) return BadRequest();
        var committed = await catalog.CommitReservationsAsync(request.ReservationIds, cancellationToken);
        return committed ? NoContent() : Conflict(new { message = "One or more reservations are missing or expired." });
    }

    /// <summary>Releases a reservation and returns its quantity to available inventory.</summary>
    [HttpPost("{reservationId:guid}/release")]
    public async Task<IActionResult> Release(Guid reservationId, CancellationToken cancellationToken)
    {
        if (!IsAuthorized()) return Unauthorized();
        await catalog.ReleaseStockAsync(reservationId, cancellationToken);
        return NoContent();
    }

    /// <summary>Compares the incoming internal-call key to Catalog's configured service key.</summary>
    private bool IsAuthorized() =>
        Request.Headers.TryGetValue("X-Service-Key", out var key) &&
        !string.IsNullOrWhiteSpace(configuration["ServiceAuth:ApiKey"]) &&
        string.Equals(key.ToString(), configuration["ServiceAuth:ApiKey"], StringComparison.Ordinal);
}
