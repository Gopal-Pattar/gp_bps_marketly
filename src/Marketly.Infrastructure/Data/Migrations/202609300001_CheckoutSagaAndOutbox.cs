using Marketly.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Marketly.Infrastructure.Data.Migrations;

[DbContext(typeof(MarketlyDbContext))]
[Migration("202609300001_CheckoutSagaAndOutbox")]
public sealed class CheckoutSagaAndOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ShippingName", table: "Orders", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "ShippingAddressLine1", table: "Orders", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "ShippingAddressLine2", table: "Orders", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "ShippingCity", table: "Orders", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "ShippingRegion", table: "Orders", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "ShippingPostalCode", table: "Orders", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "ShippingCountryCode", table: "Orders", type: "text", nullable: false, defaultValue: "IN");
        migrationBuilder.CreateTable(
            name: "CheckoutSagas",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                RequestJson = table.Column<string>(type: "text", nullable: false),
                OrderJson = table.Column<string>(type: "text", nullable: true),
                PaymentProvider = table.Column<string>(type: "text", nullable: true),
                PaymentProviderOrderId = table.Column<string>(type: "text", nullable: true),
                PaymentPublicKeyId = table.Column<string>(type: "text", nullable: true),
                PaymentAmountSubunits = table.Column<long>(type: "bigint", nullable: true),
                PaymentCurrency = table.Column<string>(type: "text", nullable: true),
                PaymentExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                ReceiptJson = table.Column<string>(type: "text", nullable: true),
                Error = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RetryAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_CheckoutSagas", x => x.Id));
        migrationBuilder.CreateIndex("IX_CheckoutSagas_CustomerId_IdempotencyKey", "CheckoutSagas", new[] { "CustomerId", "IdempotencyKey" }, unique: true);
        migrationBuilder.CreateIndex("IX_CheckoutSagas_State_RetryAfter", "CheckoutSagas", new[] { "State", "RetryAfter" });
        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Payload = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeadLetteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                LastError = table.Column<string>(type: "text", nullable: true),
                LockId = table.Column<string>(type: "text", nullable: true),
                LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_OutboxMessages", x => x.Id));
        migrationBuilder.CreateIndex("IX_OutboxMessages_MessageId", "OutboxMessages", "MessageId", unique: true);
        migrationBuilder.CreateIndex("IX_OutboxMessages_ProcessedAt_NextAttemptAt_CreatedAt", "OutboxMessages", new[] { "ProcessedAt", "NextAttemptAt", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("CheckoutSagas");
        migrationBuilder.DropTable("OutboxMessages");
        migrationBuilder.DropColumn("ShippingName", "Orders");
        migrationBuilder.DropColumn("ShippingAddressLine1", "Orders");
        migrationBuilder.DropColumn("ShippingAddressLine2", "Orders");
        migrationBuilder.DropColumn("ShippingCity", "Orders");
        migrationBuilder.DropColumn("ShippingRegion", "Orders");
        migrationBuilder.DropColumn("ShippingPostalCode", "Orders");
        migrationBuilder.DropColumn("ShippingCountryCode", "Orders");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder) => MarketlyDbContextModelSnapshot.Configure(modelBuilder);
}
