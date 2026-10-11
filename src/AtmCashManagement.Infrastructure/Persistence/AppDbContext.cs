using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Domain.Atms;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Atm> Atms => Set<Atm>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
