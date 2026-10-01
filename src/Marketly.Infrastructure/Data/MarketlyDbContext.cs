using Marketly.Domain;
using Marketly.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Marketly.Infrastructure.Data;

public sealed class MarketlyDbContext(DbContextOptions<MarketlyDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<CheckoutSaga> CheckoutSagas => Set<CheckoutSaga>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Order>().Property(x => x.Subtotal).HasPrecision(12, 2);
        model.Entity<Order>().Property(x => x.Shipping).HasPrecision(12, 2);
        model.Entity<Order>().Property(x => x.Tax).HasPrecision(12, 2);
        model.Entity<Order>().Property(x => x.Total).HasPrecision(12, 2);
        model.Entity<OrderItem>().Property(x => x.UnitPrice).HasPrecision(12, 2);
        model.Entity<OrderItem>().HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<CheckoutSaga>().HasIndex(x => new { x.CustomerId, x.IdempotencyKey }).IsUnique();
        model.Entity<CheckoutSaga>().HasIndex(x => new { x.State, x.RetryAfter });
        model.Entity<CheckoutSaga>().Property(x => x.IdempotencyKey).HasMaxLength(128);
        model.Entity<CheckoutSaga>().Property(x => x.RequestHash).HasMaxLength(64);
        model.Entity<CheckoutSaga>().Property(x => x.State).HasMaxLength(40);
        model.Entity<OutboxMessage>().HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt, x.CreatedAt });
        model.Entity<OutboxMessage>().HasIndex(x => x.MessageId).IsUnique();
        model.Entity<OutboxMessage>().Property(x => x.Type).HasMaxLength(200);
        model.Entity<OutboxMessage>().Property(x => x.ContentType).HasMaxLength(100);
    }
}

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public required string Type { get; set; }
    public required string ContentType { get; set; }
    public required string Payload { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime? DeadLetteredAt { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public string? LockId { get; set; }
    public DateTime? LockedUntil { get; set; }
}
