using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Persistence;

namespace WasteSubmitRegPackaging.SubmissionPeriods.Infrastructure;

/// <summary>
/// Reads the <c>period_*</c> tables, read-only: periods are changed by Liquibase changesets only. It maps no entities,
/// because the module's one query is written in SQL (<c>Database.SqlQuery</c>) into row types.
/// </summary>
internal sealed class SubmissionPeriodsDbContext(DbContextOptions<SubmissionPeriodsDbContext> options) : ReadOnlyDbContext(options)
{
    protected override string ChangelogFolder => "changelog/changes/submission-periods/";
}
