using Marketly.Application.DTOs;

namespace Marketly.Api.DTOs;

/// <summary>Browser/API payload used to start checkout with items and delivery details.</summary>
public sealed record CheckoutRequest(List<CheckoutLine>? Items, CheckoutAddress? DeliveryAddress);

/// <summary>Razorpay Checkout.js callback values; Orders revalidates them with Razorpay.</summary>
public sealed record RazorpayConfirmationRequest(string RazorpayOrderId, string RazorpayPaymentId, string RazorpaySignature);
