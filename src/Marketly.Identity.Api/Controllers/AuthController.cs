using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Marketly.Identity.Api.DTOs;
using Marketly.Identity.Api.Data;
using Marketly.Identity.Api.Entities;

namespace Marketly.Identity.Api.Controllers;

/// <summary>Registration, login, and current-customer endpoints for Identity.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IdentityDbContext db,
    IPasswordHasher<CustomerAccount> passwordHasher,
    IConfiguration configuration,
    IMapper mapper) : ControllerBase
{
    /// <summary>Creates a customer with a one-way password hash and returns a signed JWT.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Length > 80 ||
            !IsEmail(request.Email) || request.Password.Length is < 10 or > 128)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["account"] = ["Enter a name, valid email, and password of 10 to 128 characters."]
            }));

        var email = request.Email.Trim().ToUpperInvariant();
        if (await db.Customers.AnyAsync(x => x.NormalizedEmail == email, cancellationToken))
            return Conflict(new { message = "An account with this email already exists." });

        var customer = new CustomerAccount
        {
            FirstName = request.FirstName.Trim(),
            Email = request.Email.Trim(),
            NormalizedEmail = email
        };
        customer.PasswordHash = passwordHasher.HashPassword(customer, request.Password);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        return Created("/api/auth/me", CreateTokenResponse(customer));
    }

    /// <summary>Checks credentials and issues the same token format used after registration.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (!IsEmail(request.Email) || string.IsNullOrEmpty(request.Password)) return Unauthorized();
        var email = request.Email.Trim().ToUpperInvariant();
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.NormalizedEmail == email, cancellationToken);
        if (customer is null || passwordHasher.VerifyHashedPassword(customer, customer.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized();
        return Ok(CreateTokenResponse(customer));
    }

    /// <summary>Returns identity claims from a valid bearer token without querying private fields.</summary>
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        id = User.FindFirstValue(ClaimTypes.NameIdentifier),
        email = User.FindFirstValue(ClaimTypes.Email),
        firstName = User.FindFirstValue(ClaimTypes.GivenName)
    });

    /// <summary>Applies a basic email-format and maximum-length check.</summary>
    private static bool IsEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 320 &&
        System.Text.RegularExpressions.Regex.IsMatch(value, @"^[^\s@]+@[^\s@]+\.[^\s@]+$");

    /// <summary>Signs a short-lived JWT and returns it with the safe customer profile.</summary>
    private AuthTokenResponse CreateTokenResponse(CustomerAccount customer)
    {
        var secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is required.");
        var issuer = configuration["Jwt:Issuer"] ?? "marketly";
        var audience = configuration["Jwt:Audience"] ?? "marketly-web";
        var expires = DateTime.UtcNow.AddHours(2);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new Claim(ClaimTypes.Email, customer.Email),
            new Claim(ClaimTypes.GivenName, customer.FirstName)
        };
        var token = new JwtSecurityToken(issuer, audience, claims, expires: expires, signingCredentials: credentials);
        return new AuthTokenResponse(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", expires,
            mapper.Map<CustomerProfileResponse>(customer));
    }
}
