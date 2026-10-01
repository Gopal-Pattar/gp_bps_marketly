using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Marketly.Notifications.Worker.Data;

namespace Marketly.Notifications.Worker.Data.Migrations;

[DbContext(typeof(NotificationsDbContext))]
[Migration("202609300001_NotificationInbox")]
public sealed class NotificationInbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Inbox",
            columns: table => new
            {
                Consumer = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Inbox", x => new { x.Consumer, x.MessageId }));
        migrationBuilder.CreateTable(
            name: "NotificationRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                Total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                OrderCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_NotificationRequests", x => x.Id));
        migrationBuilder.CreateIndex("IX_NotificationRequests_MessageId", "NotificationRequests", "MessageId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Inbox");
        migrationBuilder.DropTable("NotificationRequests");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder) => NotificationDbContextModelSnapshot.Configure(modelBuilder);
}
