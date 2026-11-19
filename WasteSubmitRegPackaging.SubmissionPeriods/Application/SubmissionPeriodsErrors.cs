using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.SubmissionPeriods.Application;

internal static class SubmissionPeriodsErrors
{
    public static readonly Error ProducerSizeNotForComplianceSchemes = Error.Validation(
        "submission_periods.producer_size_not_for_compliance_schemes",
        "A producer size only applies to a direct producer: a compliance scheme reports for large and small members.");

    /// <summary>The submission period doesn't exist, isn't visible yet, or has no data periods for this submitter.</summary>
    public static Error PackagingSubmissionPeriodNotAvailable(string submissionPeriod) => Error.NotFound(
        "submission_periods.packaging_submission_period_not_available",
        $"Packaging submission period '{submissionPeriod}' isn't available to this submitter.");
}
