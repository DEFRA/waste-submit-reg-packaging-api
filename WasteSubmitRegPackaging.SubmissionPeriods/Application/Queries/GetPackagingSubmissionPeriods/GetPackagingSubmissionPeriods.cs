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
        // The {values} are sent as parameters, never pasted into the SQL. Each row carries its submission period's
        // name and dates, so a code's own row is enough to say which submission period it's in.
        var rows = await dbContext.Database.SqlQuery<PackagingSubmissionPeriodRow>($"""
            SELECT p.code, p.description, p.start_date, p.end_date, p.producer_size,
                   p.submission_period, p.opening_date, p.visible_from_date, p.deadline_date, p.registration_year
            FROM period_packaging_submission_period p
            WHERE {query.PeriodCode}::text IS NULL OR p.code = {query.PeriodCode}
            ORDER BY p.opening_date, p.start_date, p.end_date, p.code
            """).ToListAsync(cancellationToken);

        return Result<IReadOnlyList<PackagingSubmissionPeriod>>.Success(PackagingSubmissionPeriodRow.ToSubmissionPeriods(rows));
    }
}
