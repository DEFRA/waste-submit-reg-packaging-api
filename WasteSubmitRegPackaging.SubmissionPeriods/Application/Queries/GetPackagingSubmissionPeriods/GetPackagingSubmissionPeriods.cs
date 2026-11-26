using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;
using WasteSubmitRegPackaging.SubmissionPeriods.Contracts;
using WasteSubmitRegPackaging.SubmissionPeriods.Infrastructure;

namespace WasteSubmitRegPackaging.SubmissionPeriods.Application.Queries.GetPackagingSubmissionPeriods;

/// <param name="PeriodCode">Only this data period code (for example 2025-P0), with its submission period. Null for all of them.</param>
internal sealed record GetPackagingSubmissionPeriodsQuery(string? PeriodCode = null)
    : IQuery<IReadOnlyList<PackagingSubmissionPeriod>>;

/// <summary>
/// Returns the packaging submission periods, each with the data periods it accepts. With a code, returns only that
/// data period's submission period, holding just that data period. Ordered by opening date, so oldest first; data
/// periods by their dates.
/// <para>
/// For shared lookup data, the contract records are also the API's response shape: the data is the same for every
/// caller, and both have to stay stable anyway.
/// </para>
/// </summary>
internal sealed class GetPackagingSubmissionPeriodsHandler(SubmissionPeriodsDbContext dbContext)
    : IQueryHandler<GetPackagingSubmissionPeriodsQuery, IReadOnlyList<PackagingSubmissionPeriod>>
{
    public async Task<Result<IReadOnlyList<PackagingSubmissionPeriod>>> HandleAsync(
        GetPackagingSubmissionPeriodsQuery query, CancellationToken cancellationToken)
    {
        // The {values} are sent as parameters, never pasted into the SQL. Both tables are small, so they're read
        // separately and paired up; submission periods left with no data periods are dropped.
        var periods = await dbContext.Database.SqlQuery<PackagingSubmissionPeriodRow>($"""
            SELECT s.name, s.year, s.visible_from_date, s.opening_date, s.deadline_date, s.registration_year
            FROM period_packaging_submission_period s
            ORDER BY s.opening_date, s.name
            """).ToListAsync(cancellationToken);

        var dataPeriods = await dbContext.Database.SqlQuery<PackagingDataPeriodRow>($"""
            SELECT d.code, d.description, d.start_date, d.end_date, d.producer_size, d.submission_period
            FROM period_packaging_data_period d
            WHERE {query.PeriodCode}::text IS NULL OR d.code = {query.PeriodCode}
            ORDER BY d.start_date, d.end_date, d.code
            """).ToListAsync(cancellationToken);

        return Result<IReadOnlyList<PackagingSubmissionPeriod>>.Success(
            PackagingSubmissionPeriodRow.ToSubmissionPeriods(periods, dataPeriods));
    }
}
