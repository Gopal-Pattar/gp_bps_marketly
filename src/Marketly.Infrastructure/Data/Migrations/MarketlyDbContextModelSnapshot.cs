using Marketly.Domain;
using Marketly.Application.Interfaces;
using Marketly.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Marketly.Infrastructure.Data.Migrations;

[DbContext(typeof(MarketlyDbContext))]
public sealed class MarketlyDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder) => Configure(modelBuilder);

    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");
        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedOnAdd().HasColumnType("uuid");
            entity.Property(x => x.CustomerId).HasColumnType("uuid");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.Status).IsRequired().HasColumnType("text");
            entity.Property(x => x.PaymentReference).IsRequired().HasColumnType("text");
            entity.Property(x => x.ShippingName).IsRequired().HasColumnType("text");
            entity.Property(x => x.ShippingAddressLine1).IsRequired().HasColumnType("text");
            entity.Property(x => x.ShippingAddressLine2).IsRequired().HasColumnType("text");
            entity.Property(x => x.ShippingCity).IsRequired().HasColumnType("text");
            entity.Property(x => x.ShippingRegion).IsRequired().HasColumnType("text");
            entity.Property(x => x.ShippingPostalCode).IsRequired().HasColumnType("text");
            entity.Property(x => x.ShippingCountryCode).IsRequired().HasColumnType("text");
            entity.Property(x => x.Subtotal).HasPrecision(12, 2).HasColumnType("numeric(12,2)");
            entity.Property(x => x.Shipping).HasPrecision(12, 2).HasColumnType("numeric(12,2)");
            entity.Property(x => x.Tax).HasPrecision(12, 2).HasColumnType("numeric(12,2)");
            entity.Property(x => x.Total).HasPrecision(12, 2).HasColumnType("numeric(12,2)");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CustomerId);
            entity.ToTable("Orders");
        });
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedOnAdd().HasColumnType("uuid");
            entity.Property(x => x.OrderId).HasColumnType("uuid");
            entity.Property(x => x.ProductId).HasColumnType("uuid");
            entity.Property(x => x.Quantity).HasColumnType("integer");
            entity.Property(x => x.UnitPrice).HasPrecision(12, 2).HasColumnType("numeric(12,2)");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => x.ProductId);
            entity.ToTable("OrderItems");
            entity.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        });
        modelBuilder.Entity<CheckoutSaga>(entity =>
        {
            entity.Property(x => x.Id).HasColumnType("uuid");
            entity.Property(x => x.CustomerId).HasColumnType("uuid");
            entity.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128).HasColumnType("character varying(128)");
            entity.Property(x => x.RequestHash).IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            entity.Property(x => x.RequestJson).IsRequired().HasColumnType("text");
            entity.Property(x => x.OrderJson).HasColumnType("text");
            entity.Property(x => x.PaymentProvider).HasColumnType("text");
            entity.Property(x => x.PaymentProviderOrderId).HasColumnType("text");
            entity.Property(x => x.PaymentPublicKeyId).HasColumnType("text");
            entity.Property(x => x.PaymentAmountSubunits).HasColumnType("bigint");
            entity.Property(x => x.PaymentCurrency).HasColumnType("text");
            entity.Property(x => x.PaymentExpiresAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.State).IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            entity.Property(x => x.OrderId).HasColumnType("uuid");
            entity.Property(x => x.ReceiptJson).HasColumnType("text");
            entity.Property(x => x.Error).HasColumnType("text");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.RetryAfter).HasColumnType("timestamp with time zone");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CustomerId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => new { x.State, x.RetryAfter });
            entity.ToTable("CheckoutSagas");
        });
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.Property(x => x.Id).HasColumnType("uuid");
            entity.Property(x => x.MessageId).HasColumnType("uuid");
            entity.Property(x => x.Type).IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
            entity.Property(x => x.ContentType).IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            entity.Property(x => x.Payload).IsRequired().HasColumnType("text");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.ProcessedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.DeadLetteredAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.NextAttemptAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.Attempts).HasColumnType("integer");
            entity.Property(x => x.LastError).HasColumnType("text");
            entity.Property(x => x.LockId).HasColumnType("text");
            entity.Property(x => x.LockedUntil).HasColumnType("timestamp with time zone");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.MessageId).IsUnique();
            entity.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt, x.CreatedAt });
            entity.ToTable("OutboxMessages");
        });
    }
}
