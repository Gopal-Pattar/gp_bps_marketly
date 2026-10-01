using Marketly.Catalog.Api.Data;
using Marketly.Catalog.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Marketly.Catalog.Api.Data.Migrations;

[DbContext(typeof(CatalogDbContext))]
[Migration("202609290001_InitialCatalog")]
public sealed class InitialCatalog : Migration
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => CatalogDbContextModelSnapshot.Configure(modelBuilder);

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Products",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Inventory = table.Column<int>(type: "integer", nullable: false),
                ImageUrl = table.Column<string>(type: "text", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Products", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_Products_Sku", table: "Products", column: "Sku", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Products_Category_IsActive", table: "Products", columns: new[] { "Category", "IsActive" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "Products");
}
