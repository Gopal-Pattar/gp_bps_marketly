using System.Security.Claims;
using Marketly.Application.DTOs;
using Marketly.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Marketly.Api.DTOs;

namespace Marketly.Api.Controllers;

/// <summary>Checkout and order-history HTTP endpoints for the Orders service.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class CheckoutController(ICheckoutService checkout, IOrderService orders, IWebHostEnvironment environment) : ControllerBase
{
    /// <summary>Validates a cart and starts or resumes its idempotent checkout saga.</summary>
    [HttpPost("checkout")]
    public async Task<IActionResult> StartCheckout(
        [FromBody] CheckoutRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId)) return Unauthorized();
        if (request.Items is null || request.Items.Count is 0 or > 30 || request.DeliveryAddress is null)
            return BadRequest(new { message = "Add 1 to 30 items and a delivery address." });
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { message = "Idempotency-Key header is required." });

        var result = await checkout.CheckoutAsync(customerId, request.Items, request.DeliveryAddress, idempotencyKey, cancellationToken);
        if (result.Pending)
            return Accepted(value: new { sagaId = result.SagaId, status = "AwaitingPayment", payment = result.Payment, retryWithSameIdempotencyKey = true });
        if (!result.Succeeded) return Conflict(new { message = result.Error });

        var order = result.Order!;
        return Created($"/api/orders/{order.Id}", order);
    }

    /// <summary>Verifies the Razorpay callback with the provider and finishes the checkout saga.</summary>
    [HttpPost("checkout/{sagaId:guid}/payment")]
    public async Task<IActionResult> ConfirmPayment(Guid sagaId, [FromBody] RazorpayConfirmationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId)) return Unauthorized();
        var result = await checkout.ConfirmPaymentAsync(customerId, sagaId, request.RazorpayOrderId,
            request.RazorpayPaymentId, request.RazorpaySignature, cancellationToken);
        if (result.Pending) return Accepted(value: new { sagaId, status = "Processing" });
        if (!result.Succeeded) return Conflict(new { message = result.Error });
        return Ok(result.Order);
    }

    /// <summary>Approves a local simulated payment; this route is unavailable outside Development.</summary>
    [HttpPost("checkout/{sagaId:guid}/simulate-payment")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> SimulatePayment(Guid sagaId, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return NotFound();
        if (!TryGetCustomerId(out var customerId)) return Unauthorized();
        var result = await checkout.ConfirmSimulatedPaymentAsync(customerId, sagaId, cancellationToken);
        if (result.Pending) return Accepted(value: new { sagaId, status = "Processing" });
        if (!result.Succeeded) return Conflict(new { message = result.Error });
        return Ok(result.Order);
    }

    /// <summary>Returns the authenticated customer's previous orders.</summary>
    [HttpGet("orders")]
    public async Task<IActionResult> GetMyOrders(CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId)) return Unauthorized();
        return Ok(await orders.GetForCustomerAsync(customerId, cancellationToken));
    }

    private bool TryGetCustomerId(out Guid customerId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out customerId);
}
