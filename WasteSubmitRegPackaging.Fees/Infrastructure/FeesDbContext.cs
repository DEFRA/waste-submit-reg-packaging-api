using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WasteSubmitRegPackaging.Fees.Domain;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Persistence;

namespace WasteSubmitRegPackaging.Fees.Infrastructure;

/// <summary>Maps the <c>fee_*</c> tables, read-only: rates are changed by Liquibase changesets only.</summary>
internal sealed class FeesDbContext(DbContextOptions<FeesDbContext> options) : ReadOnlyDbContext(options)
{
    protected override string ChangelogFolder => "changelog/changes/fees/";

    public DbSet<FeeGroup> FeeGroups => Set<FeeGroup>();

    public DbSet<FeeRate> FeeRates => Set<FeeRate>();
}

// The EF mappings. Column names come from the snake_case convention and must match
// changelog/changes/fees/; FeesSchemaTest checks this against the real changelog.

internal sealed class FeeGroupConfiguration : IEntityTypeConfiguration<FeeGroup>
{
    public void Configure(EntityTypeBuilder<FeeGroup> builder)
    {
        builder.ToTable("fee_group");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
    }
}

internal sealed class FeeRateConfiguration : IEntityTypeConfiguration<FeeRate>
{
    public void Configure(EntityTypeBuilder<FeeRate> builder)
    {
        builder.ToTable("fee_rate");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Amount).HasPrecision(19, 4);
    }
}
