using Marketly.Identity.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Marketly.Identity.Api.Data;

/// <summary>EF Core context for customer identity data.</summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<CustomerAccount> Customers => Set<CustomerAccount>();

    /// <summary>Enforces unique normalized emails and bounded profile columns.</summary>
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<CustomerAccount>().HasIndex(x => x.NormalizedEmail).IsUnique();
        model.Entity<CustomerAccount>().Property(x => x.Email).HasMaxLength(320);
        model.Entity<CustomerAccount>().Property(x => x.NormalizedEmail).HasMaxLength(320);
        model.Entity<CustomerAccount>().Property(x => x.FirstName).HasMaxLength(80);
    }
}
