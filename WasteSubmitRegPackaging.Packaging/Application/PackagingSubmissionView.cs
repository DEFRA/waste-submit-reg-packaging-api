using WasteSubmitRegPackaging.Packaging.Domain;

namespace WasteSubmitRegPackaging.Packaging.Application;

/// <summary>
/// The one view of a packaging submission, returned by every read and every command, so the frontend always has the
/// status and the next step from the backend and never works them out.
/// </summary>
/// <param name="NextStep">What the producer does next. Null when there is nothing for them to do.</param>
/// <param name="Cycle">The cycle in hand: the open one, or the last one decided.</param>
internal sealed record PackagingSubmissionView(
    Guid Id,
    Guid OrganisationId,
    Guid? ComplianceSchemeId,
    string SubmissionPeriod,
    int PackagingYear,
    int RegistrationYear,
    PackagingSubmissionStatus Status,
    PackagingNextStep? NextStep,
    SubmissionCycleView Cycle)
{
    public static PackagingSubmissionView From(PackagingSubmission submission)
    {
        var cycle = submission.CurrentCycle;
        var file = submission.LatestFile;

        return new PackagingSubmissionView(
            submission.Id.Value,
            submission.OrganisationId,
            submission.ComplianceSchemeId,
            submission.SubmissionPeriod,
            submission.PackagingYear,
            submission.RegistrationYear,
            submission.Status,
            submission.NextStep(),
            new SubmissionCycleView(
                cycle.CycleNumber,
                cycle.IsResubmission,
                file is null
                    ? null
                    : new SubmissionFileView(file.Id, file.Status, file.FileName, file.ErrorCount, file.WarningCount, file.StartedAt),
                cycle.PaymentReference,
                cycle.SubmittedFileId,
                cycle.SubmittedAt,
                cycle.Decision,
                cycle.DecisionComments,
                cycle.DecidedAt));
    }
}

/// <param name="LatestFile">The newest upload in this cycle, whatever happened to it.</param>
/// <param name="PaymentReference">Resubmissions only: the payment for this cycle's fee. Null until it is recorded.</param>
internal sealed record SubmissionCycleView(
    int CycleNumber,
    bool IsResubmission,
    SubmissionFileView? LatestFile,
    string? PaymentReference,
    Guid? SubmittedFileId,
    DateTimeOffset? SubmittedAt,
    PackagingRegulatorDecision? Decision,
    string? DecisionComments,
    DateTimeOffset? DecidedAt);

internal sealed record SubmissionFileView(
    Guid Id,
    PackagingFileStatus Status,
    string? FileName,
    int? ErrorCount,
    int? WarningCount,
    DateTimeOffset StartedAt);
