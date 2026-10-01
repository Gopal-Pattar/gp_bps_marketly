using Marketly.Application.Services;
using Marketly.Application.Mapping;
using Marketly.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Marketly.Application;

public static class DependencyInjection
{
    /// <summary>Registers application use cases so API hosts depend on application interfaces.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scan only the Application assembly so this service owns and validates its mapping profiles.
        services.AddAutoMapper(_ => { }, typeof(OrderMappingProfile).Assembly);
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IOrderService, OrderService>();
        return services;
    }
}
