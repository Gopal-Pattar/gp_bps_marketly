namespace Marketly.Identity.Api.DTOs;

/// <summary>Input used to register a customer.</summary>
public sealed record RegisterRequest(string FirstName, string Email, string Password);

/// <summary>Input used to sign in an existing customer.</summary>
public sealed record LoginRequest(string Email, string Password);
