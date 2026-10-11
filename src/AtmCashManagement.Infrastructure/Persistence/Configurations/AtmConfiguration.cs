using AtmCashManagement.Domain.Atms;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtmCashManagement.Infrastructure.Persistence.Configurations;

internal sealed class AtmConfiguration : IEntityTypeConfiguration<Atm>
{
    public void Configure(EntityTypeBuilder<Atm> builder)
    {
        builder.ToTable("atms");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Code).HasMaxLength(Atm.CodeMaxLength).IsRequired();
        builder.HasIndex(a => a.Code).IsUnique();

        builder.Property(a => a.Location).HasMaxLength(Atm.LocationMaxLength).IsRequired();

        // Stored as text so the table stays readable and enum reordering cannot corrupt data.
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(a => a.CreatedAt).IsRequired();
    }
}
