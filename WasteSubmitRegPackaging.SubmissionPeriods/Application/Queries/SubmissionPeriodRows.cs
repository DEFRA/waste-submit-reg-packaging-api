using WasteSubmitRegPackaging.SubmissionPeriods.Contracts;

namespace WasteSubmitRegPackaging.SubmissionPeriods.Application.Queries;

// One row class per table, named after it: period_x_y is XYRow. Each has every column, so any query on the table can
// use it, and SQL columns map to the properties by the snake_case convention. The contract records are built from
// these rows; the rows never leave the module.

/// <summary>
/// A row of <c>period_registration_submission_period</c>: when one kind of registrant can register for a year. The
/// same row as epr-payment-service's <c>[Lookup].[SubmissionPeriod]</c>. The dates are timestamptz.
/// </summary>
internal sealed class RegistrationSubmissionPeriodRow
{
    public int Id { get; init; }
    public string WindowType { get; init; } = "";
    public int RegistrationYear { get; init; }
    public DateTimeOffset OpeningDate { get; init; }
    public DateTimeOffset DeadlineDate { get; init; }
    public DateTimeOffset ClosingDate { get; init; }

    public RegistrationSubmissionPeriod ToContract() =>
        new(Id, WindowType, RegistrationYear, OpeningDate, DeadlineDate, ClosingDate);
}

/// <summary>
/// A row of <c>period_packaging_submission_period</c>: one data period (what the data covers and who reports it) and
/// the submission period it's submitted in (when). The submission times are timestamp without time zone (UK time, as
/// in today's config), so they're DateTime.
/// </summary>
internal sealed class PackagingSubmissionPeriodRow
{
    public string Code { get; init; } = "";
    public string Description { get; init; } = "";
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string ProducerSize { get; init; } = "";
    public string SubmissionPeriod { get; init; } = "";
    public DateTime OpeningDate { get; init; }
    public DateTime VisibleFromDate { get; init; }
    public DateTime DeadlineDate { get; init; }
    public int RegistrationYear { get; init; }

    /// <summary>The short code is the part after the year: 2026-H1 is H1.</summary>
    public PackagingDataPeriod ToDataPeriod() => new(
        Code,
        Code[(Code.IndexOf('-') + 1)..],
        Description,
        StartDate,
        EndDate,
        Enum.Parse<ProducerSize>(ProducerSize));

    /// <summary>
    /// Groups rows into packaging submission periods, in the order the rows came. The submission period's dates are
    /// on every row (they're the same on each), so they're taken from the first.
    /// </summary>
    public static IReadOnlyList<PackagingSubmissionPeriod> ToSubmissionPeriods(IEnumerable<PackagingSubmissionPeriodRow> rows) =>
        rows.GroupBy(r => r.SubmissionPeriod)
            .Select(group =>
            {
                var first = group.First();
                return new PackagingSubmissionPeriod(
                    first.SubmissionPeriod,
                    group.Min(r => r.StartDate.Year),
                    first.OpeningDate,
                    first.VisibleFromDate,
                    first.DeadlineDate,
                    first.RegistrationYear,
                    group.Select(r => r.ToDataPeriod()).ToList());
            })
            .ToList();
}
