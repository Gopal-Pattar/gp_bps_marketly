using Marketly.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Marketly.Infrastructure.Data.Migrations;

[DbContext(typeof(MarketlyDbContext))]
[Migration("202609290002_DetachIdentityAndCatalog")]
public sealed class DetachIdentityAndCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$
        BEGIN
            IF to_regclass('"Orders"') IS NOT NULL AND EXISTS (
                SELECT 1 FROM pg_constraint WHERE conname = 'FK_Orders_Customers_CustomerId'
            ) THEN
                ALTER TABLE "Orders" DROP CONSTRAINT "FK_Orders_Customers_CustomerId";
            END IF;
            IF to_regclass('"OrderItems"') IS NOT NULL AND EXISTS (
                SELECT 1 FROM pg_constraint WHERE conname = 'FK_OrderItems_Products_ProductId'
            ) THEN
                ALTER TABLE "OrderItems" DROP CONSTRAINT "FK_OrderItems_Products_ProductId";
            END IF;
        END $$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Reattaching identity or catalog database constraints is not a safe automatic rollback.");

    protected override void BuildTargetModel(ModelBuilder modelBuilder) => MarketlyDbContextModelSnapshot.Configure(modelBuilder);
}
