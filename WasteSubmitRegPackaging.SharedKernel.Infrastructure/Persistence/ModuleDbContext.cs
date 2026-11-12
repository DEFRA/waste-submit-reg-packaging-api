using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Outbox;

namespace WasteSubmitRegPackaging.SharedKernel.Infrastructure.Persistence;

/// <summary>
/// The base for each module's own DbContext. Every module has its own context and table prefix
/// (<c>reg_</c>, <c>pkg_</c>), all in the one CDP database. A context only maps its own module's
/// tables, so there are no joins across modules; modules talk through their Contracts.
///
/// EF never creates or migrates the schema here. Liquibase owns it (see <c>changelog/</c>), because
/// the service runs as CDP's DML-only database user.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>The module's outbox table, for example <c>reg_outbox_message</c>.</summary>
    public abstract string OutboxTableName { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration(OutboxTableName));

        // Picks up every IEntityTypeConfiguration in the module's assembly. One context per module
        // keeps that safe.
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}
