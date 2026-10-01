using Marketly.Identity.Api;
using Marketly.Identity.Api.DTOs;
using Marketly.Identity.Api.Data;
using Marketly.Identity.Api.Entities;
using Marketly.Identity.Api.Mapping;
using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// Identity is a separate API host. This composition root configures its account store,
// JWT validation, MVC controllers, and the pipeline used by this service alone.
var builder = WebApplication.CreateBuilder(args);
// Identity scans only its own profile assembly to keep account mappings private to this service.
builder.Services.AddAutoMapper(_ => { }, typeof(IdentityMappingProfile));
var secret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is required.");
var issuer = builder.Configuration["Jwt:Issuer"] ?? "marketly";
var audience = builder.Configuration["Jwt:Audience"] ?? "marketly-web";
builder.Services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Identity")
        ?? throw new InvalidOperationException("ConnectionStrings:Identity is required.")));
builder.Services.AddScoped<IPasswordHasher<CustomerAccount>, PasswordHasher<CustomerAccount>>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    });
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
// Catch incomplete safe-profile mappings as the Identity service starts.
app.Services.GetRequiredService<AutoMapper.IConfigurationProvider>().AssertConfigurationIsValid();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

// Apply the Identity schema only when enabled by local Compose or an explicit deployment step.
if (app.Configuration.GetValue("Database:ApplyMigrations", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
}

app.Run();
