using WasteSubmitRegPackaging.SharedKernel.Domain;
using WasteSubmitRegPackaging.SharedKernel.Results;
using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackaging.Packaging.Domain;

/// <summary>
/// One submitter's packaging data for one submission period, and the single owner of its workflow.
/// See <c>docs/packaging_workflow.md</c>.
///
/// <code>
///   (no row)             --start upload--&gt; InProgress            (cycle 1)
///   InProgress           --submit--&gt;       SubmittedToRegulator
///   SubmittedToRegulator --Accepted--&gt;     AcceptedByRegulator,  --Rejected--&gt; RejectedByRegulator
///   Accepted / Rejected  --start upload--&gt; InProgress            (the next cycle)
/// </code>
///
/// It holds three things: itself (who, which period, the status), its <see cref="Files"/> (one per upload) and its
/// <see cref="Cycles"/> (one per pass from upload to decision). What the producer does next is worked out in one
/// place, <see cref="NextStep"/>, from what has been recorded. There is no status or flag per screen.
///
/// SAMPLE: the shape of the whole workflow, thin. It is expected to change as the screens are built.
/// </summary>
internal sealed class PackagingSubmission : AggregateRoot<PackagingSubmissionId>
{
    private readonly List<SubmissionFile> _files = [];
    private readonly List<SubmissionCycle> _cycles = [];

    private PackagingSubmission()
    {
        // For EF Core.
    }

    public Guid OrganisationId { get; private set; }

    /// <summary>Set when a compliance scheme submits for its members. Null for a direct producer.</summary>
    public Guid? ComplianceSchemeId { get; private set; }

    /// <summary>The submission period's name, for example "July to December 2025": the key the periods are known by.</summary>
    public string SubmissionPeriod { get; private set; } = null!;

    public int PackagingYear { get; private set; }

    /// <summary>The registration year this data is reported before, from the submission period.</summary>
    public int RegistrationYear { get; private set; }

    public PackagingSubmissionStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<SubmissionFile> Files => _files;

    public IReadOnlyList<SubmissionCycle> Cycles => _cycles;

    /// <summary>The cycle in hand: the open one, or the last one decided.</summary>
    public SubmissionCycle CurrentCycle => _cycles.MaxBy(cycle => cycle.CycleNumber)!;

    /// <summary>The newest upload in the current cycle, whatever happened to it.</summary>
    public SubmissionFile? LatestFile =>
        _files.Where(file => file.SubmissionCycleId == CurrentCycle.Id).MaxBy(file => (file.StartedAt, file.Id));

    /// <summary>
    /// Creates the submission. It is only called with <see cref="StartFileUpload"/>: a submission exists from the
    /// moment its first upload starts, and not before.
    /// </summary>
    public static PackagingSubmission Start(
        ProducerUser startedBy,
        Guid organisationId,
        Guid? complianceSchemeId,
        string submissionPeriod,
        int packagingYear,
        int registrationYear,
        DateTimeOffset now)
    {
        var submission = new PackagingSubmission
        {
            Id = PackagingSubmissionId.New(),
            OrganisationId = organisationId,
            ComplianceSchemeId = complianceSchemeId,
            SubmissionPeriod = submissionPeriod,
            PackagingYear = packagingYear,
            RegistrationYear = registrationYear,
            Status = PackagingSubmissionStatus.InProgress,
            CreatedByUserId = startedBy.UserId,
            CreatedAt = now
        };

        submission.Raise(new PackagingSubmissionStarted(submission.Id.Value, organisationId, submissionPeriod, now));
        return submission;
    }

    /// <summary>
    /// What the producer has to do next, or null when there is nothing for them to do (the status says why).
    /// Every producer step below checks it, so a step can only be taken when it is the next one.
    /// </summary>
    public PackagingNextStep? NextStep()
    {
        if (Status != PackagingSubmissionStatus.InProgress)
            return null;

        return LatestFile?.Status switch
        {
            null or PackagingFileStatus.Rejected => PackagingNextStep.UploadFile,
            // Two waits, one step: the caller re-reads until it moves on, and needn't know which it is waiting for.
            PackagingFileStatus.Uploading or PackagingFileStatus.Validating => PackagingNextStep.AwaitFileCheck,
            PackagingFileStatus.Invalid => PackagingNextStep.FixErrors,
            _ => CurrentCycle is { IsResubmission: true, PaymentReference: null }
                ? PackagingNextStep.PayResubmissionFee
                : PackagingNextStep.SubmitToRegulator
        };
    }

    /// <summary>
    /// Records that an upload has started. A producer can upload again at any point in an open cycle. After the
    /// regulator's decision, an upload opens the next cycle.
    /// </summary>
    /// <param name="callbackToken">The secret the uploader's callback must present for this file.</param>
    public Result<SubmissionFile> StartFileUpload(ProducerUser startedBy, string callbackToken, DateTimeOffset now)
    {
        // SAMPLE rule: EPR allows a new upload while the regulator is deciding. A team decision.
        if (Status == PackagingSubmissionStatus.SubmittedToRegulator)
            return PackagingErrors.UploadNotAllowed(Status);

        if (_cycles.Count == 0 || Status != PackagingSubmissionStatus.InProgress)
        {
            var isResubmission = _cycles.Any(cycle => cycle.Decision == PackagingRegulatorDecision.Accepted);
            _cycles.Add(new SubmissionCycle(_cycles.Count + 1, isResubmission, now));
            Status = PackagingSubmissionStatus.InProgress;
        }

        var file = new SubmissionFile(CurrentCycle.Id, callbackToken, startedBy.UserId, now);
        _files.Add(file);
        return file;
    }

    /// <summary>The uploader stored the file. It now waits for validation, which is a step of its own.</summary>
    public void RecordFileStored(SubmissionFile file, StoredFile stored, DateTimeOffset now) => file.Store(stored, now);

    /// <summary>The stored file has been validated: it is now Valid or Invalid.</summary>
    public Result RecordFileValidated(SubmissionFile file, PackagingFileCheck check, DateTimeOffset now)
    {
        if (file.Status != PackagingFileStatus.Validating)
            return PackagingErrors.FileNotAwaitingValidation(file.Status);

        file.RecordValidation(check, now);
        return Result.Success();
    }

    /// <summary>The uploader rejected the file, for example for a virus.</summary>
    public void RecordFileRejected(SubmissionFile file, string? fileName, string? reason, DateTimeOffset now) =>
        file.Reject(fileName, reason, now);

    /// <summary>
    /// Resubmissions only. PLACEHOLDER for the fee step: records the reference of the payment for this cycle's fee,
    /// which is what lets the cycle be submitted.
    /// </summary>
    public Result RecordResubmissionFeePayment(string paymentReference, DateTimeOffset now)
    {
        if (StepNotNext(PackagingNextStep.PayResubmissionFee) is { } error)
            return error;

        CurrentCycle.RecordPayment(paymentReference, now);
        return Result.Success();
    }

    /// <summary>The producer submits the file they have checked to the regulator.</summary>
    /// <param name="fileId">The file the producer was shown. It must still be the latest, or they'd submit one they haven't seen.</param>
    /// <param name="submittedBy">The name given on the declaration.</param>
    public Result SubmitToRegulator(ProducerUser submittedByUser, Guid fileId, string submittedBy, DateTimeOffset now)
    {
        if (StepNotNext(PackagingNextStep.SubmitToRegulator) is { } error)
            return error;

        if (LatestFile!.Id != fileId)
            return PackagingErrors.FileNotCurrent;

        CurrentCycle.Submit(fileId, submittedByUser.UserId, submittedBy, now);
        Status = PackagingSubmissionStatus.SubmittedToRegulator;

        Raise(new PackagingSubmissionSubmitted(Id.Value, CurrentCycle.CycleNumber, CurrentCycle.IsResubmission, fileId, now));
        return Result.Success();
    }

    public Result RecordRegulatorDecision(
        PackagingRegulatorDecision decision, RegulatorUser decidedBy, string? comments, DateTimeOffset now)
    {
        if (Status != PackagingSubmissionStatus.SubmittedToRegulator)
            return PackagingErrors.DecisionNotAllowed(Status);

        CurrentCycle.Decide(decision, comments, decidedBy.UserId, now);
        Status = decision == PackagingRegulatorDecision.Accepted
            ? PackagingSubmissionStatus.AcceptedByRegulator
            : PackagingSubmissionStatus.RejectedByRegulator;

        Raise(new PackagingDecisionRecorded(Id.Value, CurrentCycle.CycleNumber, decision, Status, now));
        return Result.Success();
    }

    private Error? StepNotNext(PackagingNextStep step) =>
        NextStep() == step ? null : PackagingErrors.StepNotAvailable(step, NextStep());
}
