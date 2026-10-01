using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Marketly.Notifications.Worker.Data;

#nullable disable

namespace Marketly.Notifications.Worker.Data.Migrations;

[DbContext(typeof(NotificationsDbContext))]
public sealed class NotificationDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder) => Configure(modelBuilder);

    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.Property(x => x.Consumer).IsRequired().HasMaxLength(160).HasColumnType("character varying(160)");
            entity.Property(x => x.MessageId).HasColumnType("uuid");
            entity.Property(x => x.ProcessedAt).HasColumnType("timestamp with time zone");
            entity.HasKey(x => new { x.Consumer, x.MessageId });
            entity.ToTable("Inbox");
        });
        modelBuilder.Entity<NotificationRequest>(entity =>
        {
            entity.Property(x => x.Id).HasColumnType("uuid");
            entity.Property(x => x.MessageId).HasColumnType("uuid");
            entity.Property(x => x.OrderId).HasColumnType("uuid");
            entity.Property(x => x.CustomerId).HasColumnType("uuid");
            entity.Property(x => x.Total).HasPrecision(12, 2).HasColumnType("numeric(12,2)");
            entity.Property(x => x.OrderCreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.Status).IsRequired().HasMaxLength(24).HasColumnType("character varying(24)");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.MessageId).IsUnique();
            entity.ToTable("NotificationRequests");
        });
    }
}
