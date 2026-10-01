using Marketly.Catalog.Api;
using Marketly.Catalog.Api.Data;
using Marketly.Catalog.Api.Interfaces;
using Marketly.Catalog.Api.Mapping;
using Marketly.Catalog.Api.Repositories;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

// Catalog runs as its own API process and owns its database, HTTP controllers, and stock-expiry worker.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Catalog")
        ?? throw new InvalidOperationException("ConnectionStrings:Catalog is required.")));
builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
// Catalog scans only its own mapping profiles to keep service contracts isolated.
builder.Services.AddAutoMapper(_ => { }, typeof(CatalogMappingProfile));
builder.Services.AddHostedService<ReservationExpiryWorker>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
// Validate Catalog's DTO profiles during startup rather than returning incomplete API data.
app.Services.GetRequiredService<AutoMapper.IConfigurationProvider>().AssertConfigurationIsValid();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

// Local Compose applies this service's own schema migrations and deterministic catalog seed.
if (app.Configuration.GetValue("Database:ApplyMigrations", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.MigrateAsync();
    if (app.Configuration.GetValue("Database:SeedDemoData", false))
        await CatalogSeeder.SeedAsync(db);
}

app.Run();
