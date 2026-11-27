using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql;
using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Persistence;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Packaging.Infrastructure;

/// <summary>Maps only the Packaging module's <c>pkg_*</c> tables.</summary>
internal sealed class PackagingDbContext(DbContextOptions<PackagingDbContext> options) : ModuleDbContext(options)
{
    public override string OutboxTableName => "pkg_outbox_message";

    public DbSet<PackagingSubmission> PackagingSubmissions => Set<PackagingSubmission>();
}

/// <summary>
/// The EF mapping. Column names come from the snake_case convention and must match the changesets in
/// <c>changelog/changes/packaging/</c>; the database test checks this.
///
/// Files and cycles are owned by the submission, so they are always loaded and saved with it: the aggregate is one
/// unit, and no query has to remember to include them.
/// </summary>
internal sealed class PackagingSubmissionConfiguration : IEntityTypeConfiguration<PackagingSubmission>
{
    public void Configure(EntityTypeBuilder<PackagingSubmission> builder)
    {
        builder.ToTable("pkg_packaging_submission");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new PackagingSubmissionId(value))
            .ValueGeneratedNever();

        builder.Property(s => s.SubmissionPeriod).HasMaxLength(100);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(40);

        // Optimistic concurrency on Postgres's xmin system column, as Registration.
        builder.Property<uint>("Version").IsRowVersion();

        builder.OwnsMany(s => s.Files, file =>
        {
            file.ToTable("pkg_submission_file");
            file.WithOwner().HasForeignKey("PackagingSubmissionId");
            file.HasKey(f => f.Id);

            // Ids are set in code, so EF treats a new entry in the list as an insert.
            file.Property(f => f.Id).ValueGeneratedNever();
            file.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);
            file.Property(f => f.CallbackToken).HasMaxLength(64);
            file.Property(f => f.FileName).HasMaxLength(260);
            file.Property(f => f.S3Bucket).HasColumnName("s3_bucket").HasMaxLength(100);
            file.Property(f => f.S3Key).HasColumnName("s3_key").HasMaxLength(1024);
            file.Property(f => f.RejectionReason).HasMaxLength(1000);
        });

        builder.OwnsMany(s => s.Cycles, cycle =>
        {
            cycle.ToTable("pkg_submission_cycle");
            cycle.WithOwner().HasForeignKey("PackagingSubmissionId");
            cycle.HasKey(c => c.Id);

            cycle.Property(c => c.Id).ValueGeneratedNever();
            cycle.Property(c => c.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            cycle.Property(c => c.SubmittedBy).HasMaxLength(200);
            cycle.Property(c => c.Decision).HasConversion<string>().HasMaxLength(20);
            cycle.Property(c => c.DecisionComments).HasMaxLength(4000);

            cycle.OwnsMany(c => c.FeeLines, line =>
            {
                line.ToTable("pkg_submission_fee_line");
                line.WithOwner().HasForeignKey("SubmissionCycleId");
                line.HasKey(l => l.Id);

                line.Property(l => l.Id).ValueGeneratedNever();
                line.Property(l => l.Description).HasMaxLength(255);
            });

            cycle.Navigation(c => c.FeeLines).HasField("_feeLines").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Navigation(s => s.Files).HasField("_files").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(s => s.Cycles).HasField("_cycles").UsePropertyAccessMode(PropertyAccessMode.Field);

        // Worked out from the files and cycles, not stored.
        builder.Ignore(s => s.CurrentCycle);
        builder.Ignore(s => s.LatestFile);

        // Aggregates raise domain events in memory only.
        builder.Ignore(s => s.DomainEvents);
    }
}

internal sealed class PackagingSubmissionRepository(PackagingDbContext dbContext) : IPackagingSubmissionRepository
{
    public Task<PackagingSubmission?> GetAsync(PackagingSubmissionId id, CancellationToken cancellationToken) =>
        dbContext.PackagingSubmissions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<PackagingSubmission?> FindAsync(
        Guid organisationId, Guid? complianceSchemeId, string submissionPeriod, CancellationToken cancellationToken) =>
        dbContext.PackagingSubmissions.SingleOrDefaultAsync(
            s => s.OrganisationId == organisationId
                 && s.ComplianceSchemeId == complianceSchemeId
                 && s.SubmissionPeriod == submissionPeriod,
            cancellationToken);

    public Task<PackagingSubmission?> GetByFileIdAsync(Guid fileId, CancellationToken cancellationToken) =>
        dbContext.PackagingSubmissions.SingleOrDefaultAsync(s => s.Files.Any(f => f.Id == fileId), cancellationToken);

    public void Add(PackagingSubmission submission) => dbContext.PackagingSubmissions.Add(submission);

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return PackagingErrors.ConcurrentChange;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two first uploads for the same submitter and period raced. The unique index is the real guard, and the
            // caller's retry finds the submission the other request created.
            return PackagingErrors.ConcurrentChange;
        }
    }
}
