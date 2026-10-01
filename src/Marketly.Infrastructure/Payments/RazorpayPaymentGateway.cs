using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Marketly.Application.Interfaces;
using Marketly.Application.DTOs;
using Microsoft.Extensions.Configuration;

namespace Marketly.Infrastructure.Payments;

public sealed class RazorpayPaymentGateway(HttpClient http, IConfiguration configuration) : IPaymentGateway
{
    private readonly string keyId = configuration["Razorpay:KeyId"]
        ?? throw new InvalidOperationException("Razorpay:KeyId must be configured when Payment:Provider is Razorpay.");
    private readonly string keySecret = configuration["Razorpay:KeySecret"]
        ?? throw new InvalidOperationException("Razorpay:KeySecret must be configured when Payment:Provider is Razorpay.");

    /// <summary>
    /// Creates a Razorpay order on the server. Amounts are converted from rupees to paise;
    /// only the public key ID is returned to the browser.
    /// </summary>
    public async Task<PaymentIntent> CreateIntentAsync(Guid orderId, decimal amount, CancellationToken ct)
    {
        var amountPaise = ToPaise(amount);
        using var response = await http.PostAsJsonAsync("orders", new
        {
            amount = amountPaise,
            currency = "INR",
            receipt = orderId.ToString("N"),
            partial_payment = false
        }, ct);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var providerOrderId = document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Razorpay did not return an order ID.");
        var returnedAmount = document.RootElement.GetProperty("amount").GetInt64();
        var currency = document.RootElement.GetProperty("currency").GetString() ?? "";
        if (returnedAmount != amountPaise || currency != "INR")
            throw new InvalidOperationException("Razorpay order amount or currency did not match the checkout quote.");
        return new PaymentIntent("Razorpay", providerOrderId, keyId, amountPaise, currency);
    }

    /// <summary>
    /// Verifies the Checkout.js HMAC and fetches the payment directly from Razorpay to check
    /// that it is captured and matches this order's expected amount and currency.
    /// </summary>
    public async Task<bool> VerifyPaymentAsync(Guid orderId, string expectedProviderOrderId, string paymentId,
        string signature, decimal expectedAmount, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(expectedProviderOrderId) || string.IsNullOrWhiteSpace(paymentId) ||
            string.IsNullOrWhiteSpace(signature)) return false;

        var signedPayload = Encoding.UTF8.GetBytes($"{expectedProviderOrderId}|{paymentId}");
        var expectedSignature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(keySecret), signedPayload);
        byte[] suppliedSignature;
        try { suppliedSignature = Convert.FromHexString(signature); }
        catch (FormatException) { return false; }
        if (suppliedSignature.Length != expectedSignature.Length ||
            !CryptographicOperations.FixedTimeEquals(expectedSignature, suppliedSignature)) return false;

        using var response = await http.GetAsync($"payments/{Uri.EscapeDataString(paymentId)}", ct);
        if (!response.IsSuccessStatusCode) return false;
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var payment = document.RootElement;
        return payment.TryGetProperty("status", out var status) && status.GetString() == "captured" &&
               payment.TryGetProperty("order_id", out var order) && order.GetString() == expectedProviderOrderId &&
               payment.TryGetProperty("amount", out var amount) && amount.GetInt64() == ToPaise(expectedAmount) &&
               payment.TryGetProperty("currency", out var currency) && currency.GetString() == "INR";
    }

    /// <summary>Refunds a captured payment when the checkout saga needs to compensate.</summary>
    public async Task CancelAuthorizationAsync(Guid orderId, string paymentId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(paymentId) || paymentId.StartsWith("sim_", StringComparison.Ordinal)) return;
        using var lookup = await http.GetAsync($"payments/{Uri.EscapeDataString(paymentId)}", ct);
        if (!lookup.IsSuccessStatusCode) return;
        using var document = JsonDocument.Parse(await lookup.Content.ReadAsStringAsync(ct));
        if (!document.RootElement.TryGetProperty("status", out var status) || status.GetString() != "captured") return;
        using var refund = await http.PostAsync($"payments/{Uri.EscapeDataString(paymentId)}/refund", content: null, ct);
        refund.EnsureSuccessStatusCode();
    }

    /// <summary>Converts an INR decimal amount to the integer paise required by Razorpay.</summary>
    private static long ToPaise(decimal rupees) =>
        checked((long)decimal.Round(rupees * 100m, 0, MidpointRounding.AwayFromZero));
}
