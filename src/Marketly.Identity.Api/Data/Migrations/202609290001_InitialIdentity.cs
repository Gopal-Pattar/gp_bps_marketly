using Marketly.Identity.Api.Data;
using Marketly.Identity.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Marketly.Identity.Api.Data.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("202609290001_InitialIdentity")]
public sealed class InitialIdentity : Migration
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => IdentityDbContextModelSnapshot.Configure(modelBuilder);

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateTable(
        name: "Customers",
        columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
            NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
            FirstName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
            PasswordHash = table.Column<string>(type: "text", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_Customers", x => x.Id));

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("Customers");
}
