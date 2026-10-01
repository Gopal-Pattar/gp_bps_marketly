using Marketly.Application.Services;
using Marketly.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Marketly.Infrastructure.Messaging;

public sealed class CheckoutSagaRecoveryWorker(IServiceScopeFactory scopes, ILogger<CheckoutSagaRecoveryWorker> logger) : BackgroundService
{
    /// <summary>Polls for due incomplete checkout sagas and resumes each one with isolated error handling.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var checkout = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
                var ids = await checkout.RecoverableSagaIdsAsync(stoppingToken);
                foreach (var id in ids)
                {
                    try { await checkout.ResumeAsync(id, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch (Exception ex) { logger.LogError(ex, "Could not resume checkout saga {SagaId}", id); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Checkout saga recovery cycle failed"); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
