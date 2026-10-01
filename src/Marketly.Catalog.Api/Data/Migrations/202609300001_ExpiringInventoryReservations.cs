using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Marketly.Catalog.Api.Data;
using Marketly.Catalog.Api.Data.Entities;

namespace Marketly.Catalog.Api.Data.Migrations;

[DbContext(typeof(CatalogDbContext))]
[Migration("202609300001_ExpiringInventoryReservations")]
public sealed class ExpiringInventoryReservations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Reservations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Reservations", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_Reservations_ExpiresAt", table: "Reservations", column: "ExpiresAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("Reservations");

    protected override void BuildTargetModel(ModelBuilder modelBuilder) => CatalogDbContextModelSnapshot.Configure(modelBuilder);
}
