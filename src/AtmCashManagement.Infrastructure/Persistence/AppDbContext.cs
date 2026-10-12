using AtmCashManagement.Application.Abstractions;
using AtmCashManagement.Application.Idempotency;
using AtmCashManagement.Domain.Atms;
using AtmCashManagement.Domain.Ledger;
using AtmCashManagement.Domain.Withdrawals;

using Microsoft.EntityFrameworkCore;

namespace AtmCashManagement.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Atm> Atms => Set<Atm>();

    public DbSet<Cassette> Cassettes => Set<Cassette>();

    public DbSet<CassetteBalance> CassetteBalances => Set<CassetteBalance>();

    public DbSet<CassetteMovement> CassetteMovements => Set<CassetteMovement>();

    public DbSet<WithdrawalQuote> WithdrawalQuotes => Set<WithdrawalQuote>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareLedger();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareLedger();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    // Balances and their ledger entries are written in the same SaveChanges, so they can never drift apart.
    private void PrepareLedger()
    {
        foreach (var atm in ChangeTracker.Entries<Atm>().Select(entry => entry.Entity).ToList())
        {
            CassetteMovements.AddRange(atm.DequeuePendingMovements());
        }

        if (ChangeTracker.Entries<CassetteMovement>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Cassette movements are append-only; they cannot be modified or deleted.");
        }
    }
}
