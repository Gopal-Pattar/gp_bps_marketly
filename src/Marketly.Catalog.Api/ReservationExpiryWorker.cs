using Microsoft.EntityFrameworkCore;
using Marketly.Catalog.Api.Data;
using Marketly.Catalog.Api.Data.Entities;

namespace Marketly.Catalog.Api;

public sealed class ReservationExpiryWorker(IServiceScopeFactory scopes, ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    /// <summary>Periodically releases expired active reservations so abandoned checkouts return stock.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
                var expiredIds = await db.Reservations.AsNoTracking()
                    .Where(x => x.State == "Active" && x.ExpiresAt <= DateTime.UtcNow)
                    .OrderBy(x => x.ExpiresAt).Select(x => x.Id).Take(100).ToListAsync(stoppingToken);
                foreach (var id in expiredIds)
                {
                    await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);
                    var reservation = await db.Reservations.FromSqlInterpolated($"SELECT * FROM \"Reservations\" WHERE \"Id\" = {id} FOR UPDATE")
                        .SingleOrDefaultAsync(stoppingToken);
                    if (reservation is null || reservation.State != "Active" || reservation.ExpiresAt > DateTime.UtcNow)
                    {
                        await tx.CommitAsync(stoppingToken);
                        continue;
                    }
                    reservation.State = "Expired";
                    await db.Products.Where(x => x.Id == reservation.ProductId)
                        .ExecuteUpdateAsync(update => update.SetProperty(p => p.Inventory, p => p.Inventory + reservation.Quantity), stoppingToken);
                    await db.SaveChangesAsync(stoppingToken);
                    await tx.CommitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Could not expire catalog reservations"); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
