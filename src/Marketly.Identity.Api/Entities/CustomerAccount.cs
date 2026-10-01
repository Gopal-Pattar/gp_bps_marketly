namespace Marketly.Identity.Api.Entities;

/// <summary>Credential and profile data owned by the Identity service.</summary>
public sealed class CustomerAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string FirstName { get; set; }
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
