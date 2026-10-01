using System.Security.Cryptography;
using Marketly.Application.Interfaces;
using Marketly.Application.DTOs;

namespace Marketly.Infrastructure.Payments;

/// <summary>Local demo adapter. It never contacts a processor or charges a payment method.</summary>
public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    /// <summary>Creates a deterministic local payment intent without contacting a payment provider.</summary>
    public Task<PaymentIntent> CreateIntentAsync(Guid orderId, decimal amount, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var providerOrderId = $"sim_order_{orderId:N}";
        var amountSubunits = checked((long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
        return Task.FromResult(new PaymentIntent("Simulated", providerOrderId, "", amountSubunits, "INR"));
    }

    /// <summary>Never approves provider callbacks; local approval uses the Development-only endpoint.</summary>
    public Task<bool> VerifyPaymentAsync(Guid orderId, string expectedProviderOrderId, string paymentId, string signature, decimal expectedAmount, CancellationToken ct) =>
        Task.FromResult(false);

    /// <summary>No external authorization exists to cancel in simulated mode.</summary>
    public Task CancelAuthorizationAsync(Guid orderId, string paymentId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
