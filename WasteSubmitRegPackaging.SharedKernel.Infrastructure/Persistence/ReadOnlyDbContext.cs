using Microsoft.EntityFrameworkCore;

namespace WasteSubmitRegPackaging.SharedKernel.Infrastructure.Persistence;

/// <summary>
/// The base for a module whose data is shared lookup data owned by Liquibase changesets (Fees,
/// SubmissionPeriods). It never tracks and refuses to save, so the app can't change the data by mistake. It has
/// no outbox, because lookup data raises no events.
/// </summary>
public abstract class ReadOnlyDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Where the module's changesets live, for the error message.</summary>
    protected abstract string ChangelogFolder { get; }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw ReadOnly();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw ReadOnly();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

    private InvalidOperationException ReadOnly() =>
        new($"This data is read-only. Change it with a Liquibase changeset in {ChangelogFolder}.");
}
