using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Marketly.Application.Interfaces;
using Marketly.Application.DTOs;
using Marketly.Domain;
using Microsoft.Extensions.Logging;

namespace Marketly.Application.Services;

public sealed class CheckoutService(
    IProductRepository products,
    IOrderRepository orders,
    ICheckoutSagaRepository sagas,
    IUnitOfWork unitOfWork,
    IPaymentGateway paymentGateway,
    IEventPublisher events,
    ILogger<CheckoutService> logger) : ICheckoutService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Starts or resumes a checkout. The customer's idempotency key and normalized request
    /// are persisted before the saga reserves stock or creates a payment intent.
    /// </summary>
    public async Task<CheckoutResult> CheckoutAsync(Guid customerId, IReadOnlyList<CheckoutLine> lines, CheckoutAddress address, string idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
            return new(null, "Supply an Idempotency-Key header of 1 to 128 characters.");
        if (lines.Count is 0 or > 30 || lines.Any(line => line.ProductId == Guid.Empty || line.Quantity is < 1 or > 20))
            return new(null, "Add 1 to 30 items; each quantity must be from 1 to 20.");

        var normalized = lines.GroupBy(x => x.ProductId).Select(g => new CheckoutLine(g.Key, g.Sum(x => x.Quantity)))
            .OrderBy(x => x.ProductId).ToArray();
        if (normalized.Any(x => x.Quantity > 20)) return new(null, "Each product quantity must be 20 or fewer.");
        var normalizedAddress = new CheckoutAddress(address.Name.Trim(), address.AddressLine1.Trim(),
            address.AddressLine2?.Trim() ?? "", address.City.Trim(), address.Region.Trim(),
            address.PostalCode.Trim(), address.CountryCode.Trim().ToUpperInvariant());
        if (normalizedAddress.Name.Length is < 1 or > 100 ||
            normalizedAddress.AddressLine1.Length is < 1 or > 160 ||
            normalizedAddress.AddressLine2!.Length > 160 ||
            normalizedAddress.City.Length is < 1 or > 80 ||
            normalizedAddress.Region.Length is < 1 or > 80 ||
            normalizedAddress.PostalCode.Length is < 1 or > 20 ||
            normalizedAddress.CountryCode.Length != 2 || !normalizedAddress.CountryCode.All(char.IsAsciiLetter))
            return new(null, "Enter a valid delivery address with a two-letter country code.");
        var requestJson = JsonSerializer.Serialize(new CheckoutCommand(normalized, normalizedAddress), JsonOptions);
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestJson)));
        var saga = await sagas.GetOrCreateAsync(customerId, idempotencyKey.Trim(), requestHash, requestJson, ct);
        if (!string.Equals(saga.RequestHash, requestHash, StringComparison.Ordinal))
            return new(null, "This Idempotency-Key has already been used with a different checkout request.");
        return await ResumeAsync(saga.Id, ct);
    }

    /// <summary>Lists nonterminal saga IDs whose retry or expiry time has arrived.</summary>
    public async Task<IReadOnlyList<Guid>> RecoverableSagaIdsAsync(CancellationToken ct) => await sagas.GetRecoverableIdsAsync(ct);

    /// <summary>
    /// Confirms a real provider payment. The adapter verifies the signature and independently
    /// checks the provider's captured payment before the saga can commit inventory.
    /// </summary>
    public Task<CheckoutResult> ConfirmPaymentAsync(Guid customerId, Guid sagaId, string providerOrderId, string paymentId, string signature, CancellationToken ct) =>
        ConfirmPaymentCoreAsync(customerId, sagaId, providerOrderId, paymentId, signature, simulated: false, ct);

    /// <summary>Completes a local demo payment; the HTTP route exposing it is Development-only.</summary>
    public Task<CheckoutResult> ConfirmSimulatedPaymentAsync(Guid customerId, Guid sagaId, CancellationToken ct) =>
        ConfirmPaymentCoreAsync(customerId, sagaId, $"sim_order_{sagaId:N}", $"sim_pay_{sagaId:N}", "", simulated: true, ct);

    /// <summary>
    /// Serializes confirmation against the recovery worker, checks ownership and expiry,
    /// and records payment authorization before continuing the saga.
    /// </summary>
    private async Task<CheckoutResult> ConfirmPaymentCoreAsync(Guid customerId, Guid sagaId, string providerOrderId, string paymentId, string signature, bool simulated, CancellationToken ct)
    {
        await using var lease = await sagas.TryAcquireLeaseAsync(sagaId, ct);
        if (lease is null) return new(null, null, true, sagaId);
        var saga = await sagas.GetAsync(sagaId, ct);
        if (saga is null || saga.CustomerId != customerId) return new(null, "Checkout attempt was not found.");
        if (saga.State == "Completed") return new(JsonSerializer.Deserialize<OrderReceipt>(saga.ReceiptJson!, JsonOptions), null);
        if (saga.State != "AwaitingPayment") return new(null, "Checkout is not awaiting payment.");
        if (saga.PaymentExpiresAt <= DateTime.UtcNow)
        {
            var command = JsonSerializer.Deserialize<CheckoutCommand>(saga.RequestJson, JsonOptions)!;
            return await FailAsync(saga, command.Items.Select(x => ReservationId(saga.Id, x.ProductId)).ToArray(),
                saga.OrderJson is null ? null : JsonSerializer.Deserialize<Order>(saga.OrderJson, JsonOptions), "Payment session expired. Please start checkout again.", ct);
        }
        if (!string.Equals(saga.PaymentProviderOrderId, providerOrderId, StringComparison.Ordinal)) return new(null, "Payment order does not match this checkout.");
        var order = JsonSerializer.Deserialize<Order>(saga.OrderJson!, JsonOptions)!;
        if (simulated)
        {
            if (saga.PaymentProvider != "Simulated") return new(null, "Simulated payment is unavailable.");
        }
        else if (!await paymentGateway.VerifyPaymentAsync(saga.Id, providerOrderId, paymentId, signature, order.Total, ct))
            return new(null, "Payment verification failed. No order was placed.");
        order.PaymentReference = paymentId;
        order.Status = "PaymentAuthorized";
        saga.OrderJson = JsonSerializer.Serialize(order, JsonOptions);
        saga.State = "PaymentAuthorized";
        await SaveSagaAsync(saga, ct);
        return await FinishPaidAsync(saga, order, ct);
    }

    /// <summary>
    /// Advances an interrupted saga from its persisted state. A saga waiting for payment
    /// returns the same intent; it never creates a second order or silently approves payment.
    /// </summary>
    public async Task<CheckoutResult> ResumeAsync(Guid sagaId, CancellationToken ct)
    {
        var lease = await sagas.TryAcquireLeaseAsync(sagaId, ct);
        if (lease is null)
        {
            var current = await sagas.GetAsync(sagaId, ct);
            return new(null, null, true, sagaId, current?.State == "AwaitingPayment" ? IntentFromSaga(current) : null);
        }
        await using (lease)
        {
            var saga = await sagas.GetAsync(sagaId, ct);
            if (saga is null) return new(null, "Checkout attempt was not found.");
            if (saga.State == "Completed") return new(JsonSerializer.Deserialize<OrderReceipt>(saga.ReceiptJson!, JsonOptions), null);
            if (saga.State == "Failed") return new(null, saga.Error ?? "Checkout failed.");

            var checkout = JsonSerializer.Deserialize<CheckoutCommand>(saga.RequestJson, JsonOptions)
                ?? throw new InvalidOperationException($"Checkout saga {saga.Id} has an invalid persisted request.");
            var lines = checkout.Items.ToArray();
            var reservationIds = lines.Select(line => ReservationId(saga.Id, line.ProductId)).ToArray();
            Order? order = saga.OrderJson is null ? null : JsonSerializer.Deserialize<Order>(saga.OrderJson, JsonOptions);
            if (saga.State == "Compensating")
                return await FailAsync(saga, reservationIds, order, saga.Error ?? "Checkout compensation is pending.", ct);
            if (saga.State == "AwaitingPayment")
            {
                if (saga.PaymentExpiresAt <= DateTime.UtcNow)
                    return await FailAsync(saga, reservationIds, order, "Payment session expired. Please start checkout again.", ct);
                return new(null, null, true, saga.Id, IntentFromSaga(saga));
            }
            if (saga.State is not ("Started" or "Reserving" or "Reserved" or "PaymentAuthorized" or "CatalogCommitted"))
                throw new InvalidOperationException($"Unknown checkout saga state '{saga.State}'.");

            if (saga.State == "Started")
            {
                var catalog = await products.GetActiveByIdsAsync(lines.Select(x => x.ProductId).ToArray(), ct);
                if (catalog.Count != lines.Length)
                    return await FailAsync(saga, reservationIds, null, "One or more products are unavailable.", ct);
                var subtotal = lines.Sum(line => catalog[line.ProductId].Price * line.Quantity);
                var shipping = subtotal >= 500m ? 0m : 60m;
                var tax = decimal.Round(subtotal * 0.18m, 2);
                order = new Order
                {
                    Id = saga.Id,
                    CustomerId = saga.CustomerId,
                    Subtotal = subtotal,
                    Shipping = shipping,
                    Tax = tax,
                    Total = subtotal + shipping + tax,
                    Status = "PendingPayment",
                    ShippingName = checkout.DeliveryAddress.Name,
                    ShippingAddressLine1 = checkout.DeliveryAddress.AddressLine1,
                    ShippingAddressLine2 = checkout.DeliveryAddress.AddressLine2 ?? "",
                    ShippingCity = checkout.DeliveryAddress.City,
                    ShippingRegion = checkout.DeliveryAddress.Region,
                    ShippingPostalCode = checkout.DeliveryAddress.PostalCode,
                    ShippingCountryCode = checkout.DeliveryAddress.CountryCode
                };
                foreach (var line in lines)
                    order.Items.Add(new OrderItem { ProductId = line.ProductId, Quantity = line.Quantity, UnitPrice = catalog[line.ProductId].Price });
                saga.OrderJson = JsonSerializer.Serialize(order, JsonOptions);
                saga.OrderId = order.Id;
                saga.State = "Reserving";
                await SaveSagaAsync(saga, ct);
            }

            order ??= JsonSerializer.Deserialize<Order>(saga.OrderJson!, JsonOptions);
            if (saga.State == "Reserving")
            {
                for (var i = 0; i < lines.Length; i++)
                {
                    if (await products.ReserveStockAsync(reservationIds[i], lines[i].ProductId, lines[i].Quantity, ct)) continue;
                    return await FailAsync(saga, reservationIds, null, "Inventory could not be reserved. Please retry with a new key.", ct);
                }
                saga.State = "Reserved";
                await SaveSagaAsync(saga, ct);
            }

            if (saga.State == "Reserved")
            {
                PaymentIntent intent;
                try { intent = await paymentGateway.CreateIntentAsync(order!.Id, order.Total, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Could not create payment intent for checkout saga {SagaId}", saga.Id);
                    return await FailAsync(saga, reservationIds, order, "Payment could not be started. Please try checkout again.", ct);
                }
                saga.PaymentProvider = intent.Provider;
                saga.PaymentProviderOrderId = intent.ProviderOrderId;
                saga.PaymentPublicKeyId = intent.PublicKeyId;
                saga.PaymentAmountSubunits = intent.AmountSubunits;
                saga.PaymentCurrency = intent.Currency;
                saga.PaymentExpiresAt = DateTime.UtcNow.AddMinutes(15);
                saga.State = "AwaitingPayment";
                await SaveSagaAsync(saga, ct);
                return new(null, null, true, saga.Id, intent);
            }

            if (saga.State == "PaymentAuthorized")
            {
                order = JsonSerializer.Deserialize<Order>(saga.OrderJson!, JsonOptions)!;
                if (!await products.CommitReservationsAsync(reservationIds, ct))
                    return await FailAsync(saga, reservationIds, order, "Inventory reservation expired before checkout completed.", ct);
                saga.State = "CatalogCommitted";
                await SaveSagaAsync(saga, ct);
            }

            if (saga.State == "CatalogCommitted")
            {
                return await FinishPaidAsync(saga, JsonSerializer.Deserialize<Order>(saga.OrderJson!, JsonOptions)!, ct);
            }

            return new(null, null, true, saga.Id);
        }
    }

    /// <summary>
    /// Commits the reserved stock, then atomically stores the paid order, completed saga,
    /// and outbox event in the Orders database.
    /// </summary>
    private async Task<CheckoutResult> FinishPaidAsync(CheckoutSaga saga, Order order, CancellationToken ct)
    {
        var lines = JsonSerializer.Deserialize<CheckoutCommand>(saga.RequestJson, JsonOptions)!.Items;
        var reservationIds = lines.Select(line => ReservationId(saga.Id, line.ProductId)).ToArray();
        if (saga.State == "PaymentAuthorized")
        {
            if (!await products.CommitReservationsAsync(reservationIds, ct))
                return await FailAsync(saga, reservationIds, order, "Inventory reservation expired before checkout completed.", ct);
            saga.State = "CatalogCommitted";
            await SaveSagaAsync(saga, ct);
        }
        order.Status = "Paid";
        saga.OrderJson = JsonSerializer.Serialize(order, JsonOptions);
        var receipt = ToReceipt(order);
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        orders.Add(order);
        await events.PublishAsync(new OrderPlaced(order.Id, saga.CustomerId, order.Total, order.CreatedAt), ct);
        saga.State = "Completed";
        saga.ReceiptJson = JsonSerializer.Serialize(receipt, JsonOptions);
        saga.UpdatedAt = DateTime.UtcNow;
        saga.RetryAfter = null;
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(receipt, null);
    }

    /// <summary>Rebuilds the public payment intent from values saved with the saga.</summary>
    private static PaymentIntent? IntentFromSaga(CheckoutSaga saga) => saga.PaymentProviderOrderId is null ? null :
        new PaymentIntent(saga.PaymentProvider!, saga.PaymentProviderOrderId, saga.PaymentPublicKeyId ?? "", saga.PaymentAmountSubunits ?? 0, saga.PaymentCurrency ?? "INR");

    /// <summary>
    /// Releases reserved stock and refunds a captured payment when possible. If compensation
    /// fails, the saga remains retryable in the Compensating state.
    /// </summary>
    private async Task<CheckoutResult> FailAsync(CheckoutSaga saga, IReadOnlyList<Guid> reservationIds, Order? order, string reason, CancellationToken ct)
    {
        var compensationFailed = false;
        foreach (var reservationId in reservationIds.Reverse())
        {
            try { await products.ReleaseStockAsync(reservationId, ct); }
            catch (Exception ex)
            {
                compensationFailed = true;
                logger.LogError(ex, "Failed to compensate inventory reservation {ReservationId} for saga {SagaId}", reservationId, saga.Id);
            }
        }
        if (order is { PaymentReference.Length: > 0 })
        {
            try { await paymentGateway.CancelAuthorizationAsync(order.Id, order.PaymentReference, ct); }
            catch (Exception ex)
            {
                compensationFailed = true;
                logger.LogError(ex, "Failed to cancel payment authorization for saga {SagaId}", saga.Id);
            }
        }
        saga.State = compensationFailed ? "Compensating" : "Failed";
        saga.Error = reason;
        saga.UpdatedAt = DateTime.UtcNow;
        await SaveSagaAsync(saga, ct);
        return compensationFailed ? new(null, reason, true, saga.Id) : new(null, reason);
    }

    /// <summary>Persists state changes and schedules either recovery retries or payment expiry.</summary>
    private async Task SaveSagaAsync(CheckoutSaga saga, CancellationToken ct)
    {
        saga.UpdatedAt = DateTime.UtcNow;
        saga.RetryAfter = saga.State is "Failed" or "Completed" ? null :
            saga.State == "AwaitingPayment" ? saga.PaymentExpiresAt : DateTime.UtcNow.AddSeconds(10);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Creates the same reservation ID for a saga/product pair on every retry.</summary>
    private static Guid ReservationId(Guid sagaId, Guid productId)
    {
        Span<byte> bytes = stackalloc byte[32];
        sagaId.TryWriteBytes(bytes[..16]);
        productId.TryWriteBytes(bytes[16..]);
        return new Guid(SHA256.HashData(bytes)[..16]);
    }

    /// <summary>Maps the completed domain order to the compact receipt returned by checkout.</summary>
    private static OrderReceipt ToReceipt(Order order) =>
        new(order.Id, order.Status, order.PaymentReference, order.Subtotal, order.Shipping, order.Tax, order.Total, order.CreatedAt);
}
