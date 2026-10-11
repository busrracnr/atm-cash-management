using AtmCashManagement.Domain.Atms;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtmCashManagement.Infrastructure.Persistence.Configurations;

internal sealed class CassetteConfiguration : IEntityTypeConfiguration<Cassette>
{
    public void Configure(EntityTypeBuilder<Cassette> builder)
    {
        // The domain enforces these rules; the constraints stop a bug or a manual edit from breaking them.
        builder.ToTable("cassettes", table =>
        {
            table.HasCheckConstraint("ck_cassettes_slot", $"slot BETWEEN 1 AND {CassetteConfigurationRules.CassetteCount}");
            table.HasCheckConstraint(
                "ck_cassettes_definition",
                "(type = 'Deposit' AND currency IS NULL AND denomination IS NULL) OR " +
                "(type = 'Recycling' AND currency IS NOT NULL AND denomination IN (20, 50, 100, 500))");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.HasIndex(c => new { c.AtmId, c.Slot }).IsUnique();

        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(c => c.Currency).HasConversion<string>().HasMaxLength(3);

        builder.HasMany(c => c.Balances).WithOne().HasForeignKey(b => b.CassetteId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Balances).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
