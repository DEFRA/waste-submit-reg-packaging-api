using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Persistence;

namespace WasteSubmitRegPackaging.Packaging.Infrastructure;

/// <summary>Maps only the Packaging module's <c>pkg_*</c> tables.</summary>
internal sealed class PackagingDbContext(DbContextOptions<PackagingDbContext> options) : ModuleDbContext(options)
{
    public override string OutboxTableName => "pkg_outbox_message";

    public DbSet<PackagingSubmission> PackagingSubmissions => Set<PackagingSubmission>();
}

internal sealed class PackagingSubmissionConfiguration : IEntityTypeConfiguration<PackagingSubmission>
{
    public void Configure(EntityTypeBuilder<PackagingSubmission> builder)
    {
        builder.ToTable("pkg_packaging_submission");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new PackagingSubmissionId(value))
            .ValueGeneratedNever();

        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(s => s.DomainEvents);
    }
}

internal sealed class PackagingSubmissionRepository(PackagingDbContext dbContext) : IPackagingSubmissionRepository
{
    public void Add(PackagingSubmission submission) => dbContext.PackagingSubmissions.Add(submission);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
