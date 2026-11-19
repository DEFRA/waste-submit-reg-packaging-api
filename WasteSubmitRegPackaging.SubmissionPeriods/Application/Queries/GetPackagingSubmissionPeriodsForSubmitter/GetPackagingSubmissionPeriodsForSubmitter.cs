using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;
using WasteSubmitRegPackaging.SubmissionPeriods.Contracts;
using WasteSubmitRegPackaging.SubmissionPeriods.Infrastructure;

namespace WasteSubmitRegPackaging.SubmissionPeriods.Application.Queries.GetPackagingSubmissionPeriodsForSubmitter;

/// <param name="ProducerSize">A direct producer's size, if known. Must be null for a compliance scheme.</param>
/// <param name="SubmissionPeriod">Only this packaging submission period, for example "January to June 2026". Null for all of them.</param>
internal sealed record GetPackagingSubmissionPeriodsForSubmitterQuery(
    SubmitterType SubmitterType,
    ProducerSize? ProducerSize = null,
    string? SubmissionPeriod = null) : IQuery<IReadOnlyList<PackagingSubmissionPeriodTile>>;

/// <summary>
/// Returns the packaging submission periods one submitter can see now, newest year first, each with the data periods
/// that submitter can report in it:
/// <list type="bullet">
///   <item>a compliance scheme gets every data period, because it reports for large and small members;</item>
///   <item>a direct producer of known size gets only data periods for that size (a small producer: P0 only);</item>
///   <item>a direct producer of unknown size gets every data period.</item>
/// </list>
/// A submission period is visible from its <c>visible_from_date</c> and open from its <c>opening_date</c>, both UK
/// local time. A submission period left with no data periods for this submitter isn't returned.
/// </summary>
internal sealed class GetPackagingSubmissionPeriodsForSubmitterHandler(SubmissionPeriodsDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<GetPackagingSubmissionPeriodsForSubmitterQuery, IReadOnlyList<PackagingSubmissionPeriodTile>>
{
    private static readonly TimeZoneInfo s_ukTime = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public async Task<Result<IReadOnlyList<PackagingSubmissionPeriodTile>>> HandleAsync(
        GetPackagingSubmissionPeriodsForSubmitterQuery query, CancellationToken cancellationToken)
    {
        if (query is { SubmitterType: SubmitterType.ComplianceScheme, ProducerSize: not null })
            return SubmissionPeriodsErrors.ProducerSizeNotForComplianceSchemes;

        // The stored times are UK local time (timestamp without time zone), so compare with UK local now. The
        // parameter's type is set explicitly, because EF would otherwise send a DateTime as timestamp with time zone.
        var ukNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), s_ukTime).DateTime;
        var ukNowParameter = new NpgsqlParameter("uk_now", NpgsqlDbType.Timestamp) { Value = ukNow };
        var producerSize = query.ProducerSize?.ToString();

        var rows = await dbContext.Database.SqlQuery<PackagingSubmissionPeriodRow>($"""
            SELECT p.code, p.description, p.start_date, p.end_date, p.producer_size,
                   p.submission_period, p.opening_date, p.visible_from_date, p.deadline_date, p.registration_year
            FROM period_packaging_submission_period p
            WHERE p.visible_from_date <= {ukNowParameter}
              AND ({producerSize}::text IS NULL OR p.producer_size = {producerSize})
              AND ({query.SubmissionPeriod}::text IS NULL OR p.submission_period = {query.SubmissionPeriod})
            ORDER BY p.opening_date, p.start_date, p.end_date, p.code
            """).ToListAsync(cancellationToken);

        var tiles = PackagingSubmissionPeriodRow.ToSubmissionPeriods(rows)
            .OrderByDescending(p => p.Year)
            .ThenBy(p => p.OpeningDate)
            .Select(p => new PackagingSubmissionPeriodTile(
                p.Name, p.Year, p.OpeningDate, p.DeadlineDate, IsOpen: ukNow >= p.OpeningDate, p.DataPeriods))
            .ToList();

        if (query.SubmissionPeriod is { } submissionPeriod && tiles.Count == 0)
            return SubmissionPeriodsErrors.PackagingSubmissionPeriodNotAvailable(submissionPeriod);

        return tiles;
    }
}
