using System.Text.Json;
using Marketly.Application.Interfaces;
using Marketly.Infrastructure.Data;

namespace Marketly.Infrastructure.Messaging;

/// <summary>Queues an event in the caller's DbContext so it commits atomically with business data.</summary>
public sealed class EfOutboxEventPublisher(MarketlyDbContext db) : IEventPublisher
{
    /// <summary>Adds a serialized event to the current context; the caller commits it with its business data.</summary>
    public Task PublishAsync<T>(T message, CancellationToken ct)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = $"{typeof(T).Name}.v1",
            ContentType = "application/json",
            Payload = JsonSerializer.Serialize(message)
        });
        return Task.CompletedTask;
    }
}
