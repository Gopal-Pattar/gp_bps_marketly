using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Marketly.Notifications.Worker.Data;

namespace Marketly.Notifications.Worker;

public sealed class OrderPlacedConsumer(IConfiguration configuration, IServiceScopeFactory scopeFactory, ILogger<OrderPlacedConsumer> logger) : BackgroundService
{
    private const string ConsumerName = "notification-order-placed-v1";

    /// <summary>Starts the Service Bus processor and keeps it running until the worker shuts down.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration["ServiceBus:ConnectionString"];
        var fullyQualifiedNamespace = configuration["ServiceBus:FullyQualifiedNamespace"];
        if (string.IsNullOrWhiteSpace(connectionString) && string.IsNullOrWhiteSpace(fullyQualifiedNamespace))
        {
            logger.LogWarning("Service Bus is not configured; notification consumer is idle");
            return;
        }

        await using var client = !string.IsNullOrWhiteSpace(connectionString)
            ? new ServiceBusClient(connectionString)
            : new ServiceBusClient(fullyQualifiedNamespace!, new DefaultAzureCredential());
        await using var processor = client.CreateProcessor(configuration["ServiceBus:Queue"] ?? "marketly-orders",
            new ServiceBusProcessorOptions { AutoCompleteMessages = false, MaxConcurrentCalls = 8, PrefetchCount = 16 });
        processor.ProcessMessageAsync += args => HandleAsync(args, stoppingToken);
        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Service Bus consumer error in {EntityPath}", args.EntityPath);
            return Task.CompletedTask;
        };
        await processor.StartProcessingAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { await processor.StopProcessingAsync(CancellationToken.None); }
    }

    /// <summary>
    /// Validates an event, writes its inbox marker and notification request in one transaction,
    /// then acknowledges it. Duplicate messages are acknowledged without creating duplicate work.
    /// </summary>
    private async Task HandleAsync(ProcessMessageEventArgs args, CancellationToken ct)
    {
        if (!string.Equals(args.Message.Subject, "OrderPlaced.v1", StringComparison.Ordinal))
        {
            await args.DeadLetterMessageAsync(args.Message, "UnsupportedEventType", "This consumer handles OrderPlaced.v1 only.", ct);
            return;
        }
        if (!Guid.TryParse(args.Message.MessageId, out var messageId))
        {
            logger.LogError("Rejected event with non-GUID message ID {MessageId}", args.Message.MessageId);
            await args.DeadLetterMessageAsync(args.Message, "InvalidMessageId", "Outbox message IDs must be GUIDs.", ct);
            return;
        }

        OrderPlacedEvent? body;
        try { body = JsonSerializer.Deserialize<OrderPlacedEvent>(args.Message.Body.ToString(), new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Rejected malformed OrderPlaced event {MessageId}", messageId);
            await args.DeadLetterMessageAsync(args.Message, "MalformedEvent", "The event payload is not valid OrderPlaced JSON.", ct);
            return;
        }
        if (body is null || body.OrderId == Guid.Empty || body.CustomerId == Guid.Empty)
        {
            await args.DeadLetterMessageAsync(args.Message, "InvalidEvent", "Required OrderPlaced identifiers are missing.", ct);
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var duplicate = await db.Inbox.AnyAsync(x => x.Consumer == ConsumerName && x.MessageId == messageId, ct);
            if (!duplicate)
            {
                db.Inbox.Add(new InboxMessage { Consumer = ConsumerName, MessageId = messageId });
                db.NotificationRequests.Add(new NotificationRequest
                {
                    MessageId = messageId,
                    OrderId = body.OrderId,
                    CustomerId = body.CustomerId,
                    Total = body.Total,
                    OrderCreatedAt = body.CreatedAt
                });
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            await args.CompleteMessageAsync(args.Message, ct);
        }
        catch (DbUpdateException ex)
        {
            // A concurrent redelivery can race the inbox insert. The transaction makes the
            // inbox marker and durable notification request an all-or-nothing operation.
            logger.LogInformation(ex, "Concurrent duplicate event {MessageId}; checking inbox before acknowledgement", messageId);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            if (await db.Inbox.AnyAsync(x => x.Consumer == ConsumerName && x.MessageId == messageId, ct))
                await args.CompleteMessageAsync(args.Message, ct);
            else
                await args.AbandonMessageAsync(args.Message, cancellationToken: ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Could not persist notification request for event {MessageId}", messageId);
            await args.AbandonMessageAsync(args.Message, cancellationToken: ct);
        }
    }
}

public sealed record OrderPlacedEvent(Guid OrderId, Guid CustomerId, decimal Total, DateTime CreatedAt);
