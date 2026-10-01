using Microsoft.EntityFrameworkCore;

namespace Marketly.Notifications.Worker.Data;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();
    public DbSet<NotificationRequest> NotificationRequests => Set<NotificationRequest>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<InboxMessage>().HasKey(x => new { x.Consumer, x.MessageId });
        model.Entity<InboxMessage>().Property(x => x.Consumer).HasMaxLength(160);
        model.Entity<NotificationRequest>().HasIndex(x => x.MessageId).IsUnique();
        model.Entity<NotificationRequest>().Property(x => x.Status).HasMaxLength(24);
    }
}

public sealed class InboxMessage
{
    public required string Consumer { get; set; }
    public Guid MessageId { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A durable, deduplicated notification work item for a later email/SMS delivery adapter.</summary>
public sealed class NotificationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal Total { get; set; }
    public DateTime OrderCreatedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";
}
