using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;
using WasteSubmitRegPackaging.SubmissionPeriods.Contracts;
using WasteSubmitRegPackaging.SubmissionPeriods.Infrastructure;

namespace WasteSubmitRegPackaging.SubmissionPeriods.Application.Queries.GetRegistrationSubmissionPeriods;

/// <param name="RegistrationYear">Only this registration year. Null for all of them.</param>
internal sealed record GetRegistrationSubmissionPeriodsQuery(int? RegistrationYear = null)
    : IQuery<IReadOnlyList<RegistrationSubmissionPeriod>>;

/// <summary>
/// Returns the registration submission periods (one per window type and registration year), optionally for one
/// registration year, in id order: the same rows as epr-payment-service's <c>[Lookup].[SubmissionPeriod]</c>.
/// </summary>
internal sealed class GetRegistrationSubmissionPeriodsHandler(SubmissionPeriodsDbContext dbContext)
    : IQueryHandler<GetRegistrationSubmissionPeriodsQuery, IReadOnlyList<RegistrationSubmissionPeriod>>
{
    public async Task<Result<IReadOnlyList<RegistrationSubmissionPeriod>>> HandleAsync(
        GetRegistrationSubmissionPeriodsQuery query, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<RegistrationSubmissionPeriodRow>($"""
            SELECT r.id, r.window_type, r.registration_year, r.opening_date, r.deadline_date, r.closing_date
            FROM period_registration_submission_period r
            WHERE {query.RegistrationYear}::integer IS NULL OR r.registration_year = {query.RegistrationYear}
            ORDER BY r.id
            """).ToListAsync(cancellationToken);

        return rows.Select(r => r.ToContract()).ToList();
    }
}
