using System.Text.Json.Serialization;

namespace WasteSubmitRegPackaging.SubmissionPeriods.Contracts;

/// <summary>The submission periods other modules use.</summary>
public interface ISubmissionPeriodsApi
{
    /// <summary>Every packaging submission period, oldest first, each with the data periods it accepts.</summary>
    Task<IReadOnlyList<PackagingSubmissionPeriod>> GetPackagingSubmissionPeriodsAsync(CancellationToken cancellationToken);

    /// <summary>The packaging submission period a data period code (for example 2025-P0) is submitted in, holding just that data period, or null.</summary>
    Task<PackagingSubmissionPeriod?> FindPackagingSubmissionPeriodByCodeAsync(string periodCode, CancellationToken cancellationToken);

    /// <summary>The registration submission periods, optionally for one registration year, in id order.</summary>
    Task<IReadOnlyList<RegistrationSubmissionPeriod>> GetRegistrationSubmissionPeriodsAsync(
        int? registrationYear, CancellationToken cancellationToken);
}

/// <summary>
/// A registration submission period: when one kind of registrant can register for a year. The same row as
/// epr-payment-service's <c>[Lookup].[SubmissionPeriod]</c>, with the same id, so migrated registration data's
/// <c>SubmissionPeriodId</c> is this <see cref="Id"/>. The dates are midnight UTC.
/// </summary>
/// <param name="WindowType">For example DirectLargeProducer. 2024 and 2025 have just Cso and Direct.</param>
public sealed record RegistrationSubmissionPeriod(
    int Id,
    string WindowType,
    int RegistrationYear,
    DateTimeOffset OpeningDate,
    DateTimeOffset DeadlineDate,
    DateTimeOffset ClosingDate);

/// <summary>
/// A packaging submission period: when packaging data is submitted ("July to December 2025"), and the data periods
/// it accepts. As configured in the packaging frontend and the POM validation function today. The times are UK
/// local time, as written in that config.
/// </summary>
/// <param name="Name">For example "July to December 2025". It is also the key the submissions API stores today.</param>
/// <param name="Year">The year the data is for.</param>
/// <param name="RegistrationYear">The registration year this data is reported before: <paramref name="Year"/> + 1.</param>
/// <param name="DataPeriods">The data periods this submission period accepts, at least one.</param>
public sealed record PackagingSubmissionPeriod(
    string Name,
    int Year,
    DateTime OpeningDate,
    DateTime VisibleFromDate,
    DateTime DeadlineDate,
    int RegistrationYear,
    IReadOnlyList<PackagingDataPeriod> DataPeriods);

/// <summary>
/// The time a producer's packaging data covers, for example 2025-P0 (January to December 2025). The code is the value
/// in the POM file's submission period column.
/// </summary>
/// <param name="ShortCode">The part after the year, for example H1, H2 or P0, as shown to users: "January to June 2026 (H1)".</param>
/// <param name="StartDate">Inclusive.</param>
/// <param name="EndDate">Inclusive.</param>
/// <param name="ProducerSize">Who reports it: large producers (H1, H2) or small producers (P0).</param>
public sealed record PackagingDataPeriod(
    string Code, string ShortCode, string Description, DateOnly StartDate, DateOnly EndDate, ProducerSize ProducerSize);

/// <summary>Who reports a data period.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProducerSize>))]
public enum ProducerSize
{
    Large,
    Small
}

/// <summary>
/// Who is submitting packaging data: a direct producer for itself, or a compliance scheme for its members. The names
/// match the submit domain (for example the registration journeys DirectLargeProducer and CsoLargeProducer).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SubmitterType>))]
public enum SubmitterType
{
    DirectProducer,
    ComplianceScheme
}

/// <summary>
/// A packaging submission period as one submitter sees it: a tile on the packaging data page. Only periods that are
/// visible now are returned. Times are UK local time.
/// </summary>
/// <param name="Name">For example "January to June 2026". It is also the key the submissions API stores today.</param>
/// <param name="Year">The year the data is for.</param>
/// <param name="IsOpen">Whether submissions can start: true from <paramref name="OpeningDate"/>. Before that the tile is shown but locked.</param>
/// <param name="DataPeriods">The data periods this submitter can report in this period, at least one.</param>
public sealed record PackagingSubmissionPeriodTile(
    string Name,
    int Year,
    DateTime OpeningDate,
    DateTime DeadlineDate,
    bool IsOpen,
    IReadOnlyList<PackagingDataPeriod> DataPeriods);
