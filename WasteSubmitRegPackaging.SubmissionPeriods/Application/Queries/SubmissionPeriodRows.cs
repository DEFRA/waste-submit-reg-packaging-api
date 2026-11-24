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
/// A row of <c>period_packaging_submission_period</c>: when packaging data is submitted. The times are timestamp
/// without time zone (UK time, as in today's config), so they're DateTime.
/// </summary>
internal sealed class PackagingSubmissionPeriodRow
{
    public string Name { get; init; } = "";
    public int Year { get; init; }
    public DateTime VisibleFromDate { get; init; }
    public DateTime OpeningDate { get; init; }
    public DateTime DeadlineDate { get; init; }
    public int RegistrationYear { get; init; }

    /// <summary>
    /// Pairs submission periods with their data periods, keeping the order of both. A submission period with no data
    /// periods in <paramref name="dataPeriods"/> is left out.
    /// </summary>
    public static IReadOnlyList<PackagingSubmissionPeriod> ToSubmissionPeriods(
        IEnumerable<PackagingSubmissionPeriodRow> periods, IEnumerable<PackagingDataPeriodRow> dataPeriods)
    {
        var bySubmissionPeriod = dataPeriods.ToLookup(d => d.SubmissionPeriod);

        return periods
            .Where(p => bySubmissionPeriod.Contains(p.Name))
            .Select(p => new PackagingSubmissionPeriod(
                p.Name, p.Year, p.OpeningDate, p.VisibleFromDate, p.DeadlineDate, p.RegistrationYear,
                bySubmissionPeriod[p.Name].Select(d => d.ToDataPeriod()).ToList()))
            .ToList();
    }
}

/// <summary>
/// A row of <c>period_packaging_data_period</c>: what the data covers and who reports it, and the submission period
/// that accepts it.
/// </summary>
internal sealed class PackagingDataPeriodRow
{
    public string Code { get; init; } = "";
    public string Description { get; init; } = "";
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string ProducerSize { get; init; } = "";
    public string SubmissionPeriod { get; init; } = "";

    /// <summary>The short code is the part after the year: 2026-H1 is H1.</summary>
    public PackagingDataPeriod ToDataPeriod() => new(
        Code,
        Code[(Code.IndexOf('-') + 1)..],
        Description,
        StartDate,
        EndDate,
        Enum.Parse<ProducerSize>(ProducerSize));
}
