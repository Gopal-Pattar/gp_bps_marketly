using Marketly.Application.DTOs;

namespace Marketly.Application.Interfaces;

/// <summary>Application use cases for idempotent checkout and payment confirmation.</summary>
public interface ICheckoutService
{
    /// <summary>Validates and idempotently creates or resumes a checkout attempt.</summary>
    Task<CheckoutResult> CheckoutAsync(Guid customerId, IReadOnlyList<CheckoutLine> lines, CheckoutAddress address, string idempotencyKey, CancellationToken ct);
    /// <summary>Verifies a real provider callback and resumes fulfillment for this customer's saga.</summary>
    Task<CheckoutResult> ConfirmPaymentAsync(Guid customerId, Guid sagaId, string providerOrderId, string paymentId, string signature, CancellationToken ct);
    /// <summary>Approves the local demo payment flow; the HTTP endpoint is Development-only.</summary>
    Task<CheckoutResult> ConfirmSimulatedPaymentAsync(Guid customerId, Guid sagaId, CancellationToken ct);
    /// <summary>Continues a saga after interruption without duplicating reservations or orders.</summary>
    Task<CheckoutResult> ResumeAsync(Guid sagaId, CancellationToken ct);
    /// <summary>Returns nonterminal saga IDs ready for background recovery.</summary>
    Task<IReadOnlyList<Guid>> RecoverableSagaIdsAsync(CancellationToken ct);
}

/// <summary>Read use cases for an authenticated customer's order history.</summary>
public interface IOrderService
{
    /// <summary>Returns the customer's orders with delivery details and purchased lines.</summary>
    Task<IReadOnlyList<OrderSummary>> GetForCustomerAsync(Guid customerId, CancellationToken ct);
}
