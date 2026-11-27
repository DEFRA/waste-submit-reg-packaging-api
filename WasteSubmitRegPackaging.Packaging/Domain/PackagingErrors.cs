using WasteSubmitRegPackaging.SharedKernel.Domain;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Packaging.Domain;

/// <summary>
/// Every error the module can return, in one place, with stable codes. The codes are part of the API contract:
/// frontends may switch on them.
/// </summary>
internal static class PackagingErrors
{
    public static Error NotFound(PackagingSubmissionId id) =>
        Error.NotFound("packaging.not_found", $"No packaging submission with ID {id}.");

    public static readonly Error NotStarted =
        Error.NotFound("packaging.not_started", "This submitter has no packaging submission for the submission period.");

    /// <summary>Also returned for a wrong callback token, so a caller can't tell a real file id from a made-up one.</summary>
    public static readonly Error FileNotFound =
        Error.NotFound("packaging.file_not_found", "No file upload with that ID.");

    public static readonly Error NotYourOrganisation =
        Error.Forbidden("packaging.not_your_organisation", "The user does not act for this organisation.");

    public static Error UnknownSubmissionPeriod(string submissionPeriod) =>
        Error.Validation("packaging.unknown_submission_period", $"There is no packaging submission period called '{submissionPeriod}'.");

    public static Error UploadNotAllowed(PackagingSubmissionStatus status) =>
        Error.Conflict("packaging.upload_not_allowed", $"A file cannot be uploaded while the submission is {status}.");

    public static Error StepNotAvailable(PackagingNextStep step, PackagingNextStep? nextStep) =>
        Error.Conflict(
            "packaging.step_not_available",
            $"{step} is not the next step. The next step is {nextStep?.ToString() ?? "none"}.");

    public static readonly Error FileNotCurrent =
        Error.Conflict("packaging.file_not_current", "A newer file has been uploaded. Check that file before submitting.");

    public static Error DecisionNotAllowed(PackagingSubmissionStatus status) =>
        Error.Conflict("packaging.decision_not_allowed", $"A decision cannot be recorded while the submission is {status}.");

    public static readonly Error ConcurrentChange =
        Error.Conflict("packaging.concurrent_change", "Someone else changed this submission. Reload it and try again.");
}

// Raised by the aggregate and saved to pkg_outbox_message in the same transaction, for the outbox dispatcher to send on.

internal sealed record PackagingSubmissionStarted(
    Guid PackagingSubmissionId, Guid OrganisationId, string SubmissionPeriod, DateTimeOffset OccurredAt) : IDomainEvent;

internal sealed record PackagingSubmissionSubmitted(
    Guid PackagingSubmissionId, int CycleNumber, bool IsResubmission, Guid FileId, DateTimeOffset OccurredAt) : IDomainEvent;

internal sealed record PackagingDecisionRecorded(
    Guid PackagingSubmissionId,
    int CycleNumber,
    PackagingRegulatorDecision Decision,
    PackagingSubmissionStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Loads and saves the aggregate, with its files and cycles, and is used by command handlers only.
/// <see cref="SaveChangesAsync"/> returns a <see cref="Result"/>: a concurrent edit, or two requests creating the same
/// submission at once, becomes a Conflict.
/// </summary>
internal interface IPackagingSubmissionRepository
{
    Task<PackagingSubmission?> GetAsync(PackagingSubmissionId id, CancellationToken cancellationToken);

    /// <summary>The one submission a submitter has for a submission period, if any.</summary>
    Task<PackagingSubmission?> FindAsync(
        Guid organisationId, Guid? complianceSchemeId, string submissionPeriod, CancellationToken cancellationToken);

    Task<PackagingSubmission?> GetByFileIdAsync(Guid fileId, CancellationToken cancellationToken);

    void Add(PackagingSubmission submission);

    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
}
