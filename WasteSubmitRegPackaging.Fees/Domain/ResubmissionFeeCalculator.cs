using WasteSubmitRegPackaging.Fees.Contracts;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Fees.Domain;

/// <summary>
/// The packaging resubmission fee, ported from epr-payment-service (ProducerResubmissionService and
/// ComplianceSchemeResubmissionService). Pure: the rate is looked up first, so this needs no database and is
/// tested on its own.
///
/// The rules, unchanged from today:
/// <list type="bullet">
/// <item>Producer: the rate, once. (The payment service's EnableResubmissionProducerMemberCountBaseFeeMultiplication flag is off.)</item>
/// <item>Compliance scheme: the rate times the member count, which must be at least 1.</item>
/// <item>Outstanding: total less previous payments. Not clamped, so an overpayment is negative.</item>
/// <item>No rounding: everything is in pence.</item>
/// </list>
/// </summary>
internal static class ResubmissionFeeCalculator
{
    public static int FeeGroupFor(Submitter submitter) => submitter switch
    {
        Submitter.Producer => FeeIds.ProducerResubmissionGroup,
        Submitter.ComplianceScheme => FeeIds.ComplianceSchemeResubmissionGroup,
        _ => throw new ArgumentOutOfRangeException(nameof(submitter), submitter, null)
    };

    public static Result<ResubmissionFee> Calculate(
        Submitter submitter, FeeRate rate, string feeDescription, int memberCount, decimal previousPayments)
    {
        if (submitter == Submitter.ComplianceScheme && memberCount < 1)
            return FeesErrors.MemberCountInvalid;

        var quantity = submitter == Submitter.ComplianceScheme ? memberCount : 1;
        var line = new FeeLine(rate.Id, rate.FeeGroupId, feeDescription, rate.Amount, quantity, rate.Amount * quantity);

        return new ResubmissionFee(
            [line],
            TotalResubmissionFee: line.Amount,
            PreviousPayments: previousPayments,
            OutstandingPayment: line.Amount - previousPayments,
            MemberCount: memberCount);
    }
}
