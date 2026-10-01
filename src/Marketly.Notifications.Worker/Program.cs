using Marketly.Notifications.Worker;
using Marketly.Notifications.Worker.Data;
using Microsoft.EntityFrameworkCore;

// This worker is a separate executable from the APIs so queue processing scales independently.
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDbContext<NotificationsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Notifications")
        ?? throw new InvalidOperationException("ConnectionStrings:Notifications is required.")));
builder.Services.AddHostedService<OrderPlacedConsumer>();
var host = builder.Build();
if (host.Services.GetRequiredService<IConfiguration>().GetValue("Database:ApplyMigrations", false))
{
    await using var scope = host.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
}
await host.RunAsync();
