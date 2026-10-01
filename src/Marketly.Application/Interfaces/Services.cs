using Marketly.Domain;
using Marketly.Application.DTOs;

namespace Marketly.Application.Interfaces;

public interface IPaymentGateway
{
    /// <summary>Creates a provider-side order using the authoritative amount calculated by Orders.</summary>
    Task<PaymentIntent> CreateIntentAsync(Guid orderId, decimal amount, CancellationToken ct);
    /// <summary>Checks the callback and confirms payment status and amount with the provider.</summary>
    Task<bool> VerifyPaymentAsync(Guid orderId, string expectedProviderOrderId, string paymentId, string signature, decimal expectedAmount, CancellationToken ct);
    /// <summary>Reverses a completed payment when the checkout saga compensates.</summary>
    Task CancelAuthorizationAsync(Guid orderId, string paymentId, CancellationToken ct);
}

public interface IEventPublisher
{
    /// <summary>Queues an integration event for durable publication.</summary>
    Task PublishAsync<T>(T message, CancellationToken ct);
}

public interface IOutboxDispatcher
{
    /// <summary>Sends due outbox messages to the configured broker.</summary>
    Task DispatchPendingAsync(CancellationToken ct);
}
