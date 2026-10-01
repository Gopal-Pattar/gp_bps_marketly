namespace Marketly.Identity.Api.DTOs;

/// <summary>Safe customer identity fields included in the authentication response.</summary>
public sealed record CustomerProfileResponse(Guid Id, string Email, string FirstName);

/// <summary>Token envelope returned after registration or sign-in.</summary>
public sealed record AuthTokenResponse(string AccessToken, string TokenType, DateTime ExpiresAt, CustomerProfileResponse Customer);
