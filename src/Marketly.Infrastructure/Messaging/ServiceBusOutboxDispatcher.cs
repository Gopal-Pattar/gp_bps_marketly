using Azure.Messaging.ServiceBus;
using Azure.Identity;
using Marketly.Application.Interfaces;
using Marketly.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Marketly.Infrastructure.Messaging;

public sealed class ServiceBusOutboxDispatcher(
    MarketlyDbContext db,
    IConfiguration configuration,
    ILogger<ServiceBusOutboxDispatcher> logger) : IOutboxDispatcher, IAsyncDisposable
{
    private readonly string? connectionString = configuration["ServiceBus:ConnectionString"];
    private readonly string? fullyQualifiedNamespace = configuration["ServiceBus:FullyQualifiedNamespace"];
    private readonly string queue = configuration["ServiceBus:Queue"] ?? "marketly-orders";
    private readonly string lockId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
    private ServiceBusClient? client;
    private ServiceBusSender? sender;

    /// <summary>
    /// Claims a bounded outbox batch, sends each event to Service Bus, and records success
    /// or schedules retry/dead-letter metadata for failures.
    /// </summary>
    public async Task DispatchPendingAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(connectionString) && string.IsNullOrWhiteSpace(fullyQualifiedNamespace)) return;
        client ??= !string.IsNullOrWhiteSpace(connectionString)
            ? new ServiceBusClient(connectionString)
            : new ServiceBusClient(fullyQualifiedNamespace!, new DefaultAzureCredential());
        sender ??= client.CreateSender(queue);
        var claimed = await ClaimBatchAsync(ct);
        foreach (var message in claimed)
        {
            try
            {
                await sender.SendMessageAsync(new ServiceBusMessage(message.Payload)
                {
                    MessageId = message.MessageId.ToString("N"),
                    ContentType = message.ContentType,
                    Subject = message.Type
                }, ct);
                message.ProcessedAt = DateTime.UtcNow;
                message.LockId = null;
                message.LockedUntil = null;
                message.LastError = null;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                if (message.Attempts >= 20) message.DeadLetteredAt = DateTime.UtcNow;
                else message.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(900, Math.Pow(2, Math.Min(message.Attempts, 9))));
                message.LockId = null;
                message.LockedUntil = null;
                logger.LogError(ex, "Outbox event {MessageId} delivery attempt {Attempt} failed", message.MessageId, message.Attempts);
            }
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Claims due events using SKIP LOCKED so multiple dispatcher instances can run safely.</summary>
    private async Task<List<OutboxMessage>> ClaimBatchAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var messages = await db.OutboxMessages.FromSqlRaw("""
                SELECT * FROM "OutboxMessages"
                WHERE "ProcessedAt" IS NULL AND "DeadLetteredAt" IS NULL AND "NextAttemptAt" <= now()
                  AND ("LockedUntil" IS NULL OR "LockedUntil" < now())
                ORDER BY "CreatedAt"
                LIMIT 25
                FOR UPDATE SKIP LOCKED
                """).ToListAsync(ct);
        foreach (var message in messages)
        {
            message.LockId = lockId;
            message.LockedUntil = now.AddMinutes(2);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return messages;
    }

    /// <summary>Closes the cached sender and client when the scoped dispatcher is disposed.</summary>
    public async ValueTask DisposeAsync()
    {
        if (sender is not null) await sender.DisposeAsync();
        if (client is not null) await client.DisposeAsync();
    }
}

public sealed class OutboxWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxWorker> logger) : BackgroundService
{
    /// <summary>Creates a fresh dependency-injection scope for each outbox polling cycle.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
                await dispatcher.DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Outbox dispatch cycle failed"); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
