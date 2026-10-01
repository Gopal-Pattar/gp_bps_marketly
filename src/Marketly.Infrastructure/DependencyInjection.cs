using Marketly.Application.Interfaces;
using Marketly.Infrastructure.Data;
using Marketly.Infrastructure.Messaging;
using Marketly.Infrastructure.Payments;
using Marketly.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Text;

namespace Marketly.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Orders persistence, repository adapters, payment provider, Catalog HTTP client,
    /// and background outbox/saga workers with the API host's dependency injection container.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Orders")
            ?? throw new InvalidOperationException("ConnectionStrings:Orders is required.");
        services.AddDbContext<MarketlyDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICheckoutSagaRepository, CheckoutSagaRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        if (string.Equals(configuration["Payment:Provider"], "Razorpay", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IPaymentGateway, RazorpayPaymentGateway>(client =>
            {
                client.BaseAddress = new Uri("https://api.razorpay.com/v1/");
                client.Timeout = TimeSpan.FromSeconds(15);
                var keyId = configuration["Razorpay:KeyId"] ?? throw new InvalidOperationException("Razorpay:KeyId is required.");
                var keySecret = configuration["Razorpay:KeySecret"] ?? throw new InvalidOperationException("Razorpay:KeySecret is required.");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}")));
            });
        }
        else services.AddScoped<IPaymentGateway, SimulatedPaymentGateway>();
        services.AddScoped<IEventPublisher, EfOutboxEventPublisher>();
        services.AddScoped<IOutboxDispatcher, ServiceBusOutboxDispatcher>();
        services.AddHostedService<OutboxWorker>();
        services.AddHostedService<CheckoutSagaRecoveryWorker>();
        services.AddHttpClient<IProductRepository, CatalogProductClient>(client =>
            client.BaseAddress = new Uri(configuration["Services:CatalogUrl"] ?? "http://localhost:8081"));
        return services;
    }
}
