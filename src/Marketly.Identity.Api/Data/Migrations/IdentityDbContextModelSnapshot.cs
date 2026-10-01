using Marketly.Identity.Api.Data;
using Marketly.Identity.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Marketly.Identity.Api.Data.Migrations;

[DbContext(typeof(IdentityDbContext))]
public sealed class IdentityDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder) => Configure(modelBuilder);

    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");
        modelBuilder.Entity<CustomerAccount>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedOnAdd().HasColumnType("uuid");
            entity.Property(x => x.Email).IsRequired().HasMaxLength(320).HasColumnType("character varying(320)");
            entity.Property(x => x.NormalizedEmail).IsRequired().HasMaxLength(320).HasColumnType("character varying(320)");
            entity.Property(x => x.FirstName).IsRequired().HasMaxLength(80).HasColumnType("character varying(80)");
            entity.Property(x => x.PasswordHash).IsRequired().HasColumnType("text");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.NormalizedEmail).IsUnique();
            entity.ToTable("Customers");
        });
    }
}
