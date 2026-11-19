using System.Text.Json.Serialization;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Fees.Contracts;

/// <summary>
/// The fee calculations other modules use. Fees works out what is charged and returns it as fee lines;
/// it stores nothing per submission. The workflow module that asked (Packaging now, Registration later)
/// saves the lines as its own fee snapshot, in the same transaction as its status change.
/// </summary>
public interface IFeesApi
{
    /// <summary>
    /// The packaging resubmission fee, ported from epr-payment-service with the same results for the same
    /// inputs. Fails with <c>fees.no_rate_for_date</c> when no rate covers the date, and
    /// <c>fees.member_count_invalid</c> when a compliance scheme has no members.
    /// </summary>
    Task<Result<ResubmissionFee>> CalculatePackagingResubmissionFeeAsync(
        PackagingResubmissionFeeInput input, CancellationToken cancellationToken);
}

/// <summary>The regulator for each nation. The numbers are the <c>fee_regulator</c> ids (the Nations ids).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RegulatorNation>))]
public enum RegulatorNation
{
    [JsonStringEnumMemberName("GB-ENG")] England = 1,
    [JsonStringEnumMemberName("GB-SCT")] Scotland = 2,
    [JsonStringEnumMemberName("GB-WLS")] Wales = 3,
    [JsonStringEnumMemberName("GB-NIR")] NorthernIreland = 4
}

/// <summary>Who is resubmitting, which decides the fee type.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Submitter>))]
public enum Submitter
{
    /// <summary>A direct producer: a flat fee per resubmission.</summary>
    Producer,

    /// <summary>A compliance scheme: a fee per member.</summary>
    ComplianceScheme
}

/// <param name="ResubmissionDate">Matched to a rate by its UTC calendar date, as in the payment service.</param>
/// <param name="MemberCount">The scheme's members. Ignored for a producer, whose fee is flat.</param>
/// <param name="PreviousPayments">
/// What has already been paid against this submission, in pence. Payments stay in epr-payment-service until
/// they move, so the caller supplies it.
/// </param>
public sealed record PackagingResubmissionFeeInput(
    Submitter Submitter,
    RegulatorNation Regulator,
    DateTimeOffset ResubmissionDate,
    int MemberCount,
    decimal PreviousPayments);

/// <summary>
/// One charged item, in pence. The same shape for every fee type, so it maps straight onto a workflow's fee
/// line item table (the shape epr-payment-service's <c>RegistrationFeeLineItem</c> uses).
/// </summary>
/// <param name="FeeRateId">The <c>fee_rate</c> row used.</param>
/// <param name="FeeGroupId">The fee type (<c>fee_group</c>), for example 5 ProducerResubmission.</param>
/// <param name="Description">The fee type's description at the time, copied so later changes don't alter it.</param>
public sealed record FeeLine(
    int FeeRateId,
    int FeeGroupId,
    string Description,
    decimal UnitPrice,
    int Quantity,
    decimal Amount);

/// <param name="OutstandingPayment">The total less previous payments. It can be negative (an overpayment), as today.</param>
public sealed record ResubmissionFee(
    IReadOnlyList<FeeLine> Lines,
    decimal TotalResubmissionFee,
    decimal PreviousPayments,
    decimal OutstandingPayment,
    int MemberCount);
