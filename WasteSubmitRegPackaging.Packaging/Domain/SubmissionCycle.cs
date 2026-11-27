namespace WasteSubmitRegPackaging.Packaging.Domain;

/// <summary>
/// One pass from upload to the regulator's decision (<c>pkg_submission_cycle</c>). Cycle 1 is the first submission.
/// Each later upload after a decision opens the next cycle, so a resubmission never overwrites what went before.
/// </summary>
internal sealed class SubmissionCycle
{
    private SubmissionCycle()
    {
        // For EF Core.
    }

    internal SubmissionCycle(int cycleNumber, bool isResubmission, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        CycleNumber = cycleNumber;
        IsResubmission = isResubmission;
        OpenedAt = now;
    }

    public Guid Id { get; private init; }

    public int CycleNumber { get; private init; }

    /// <summary>
    /// True when an earlier cycle was accepted, which is EPR's rule today. Only a resubmission has a fee. A cycle
    /// that follows a rejection is not a resubmission.
    /// </summary>
    public bool IsResubmission { get; private init; }

    public DateTimeOffset OpenedAt { get; private init; }

    /// <summary>
    /// Resubmissions only: the reference of the payment for this cycle's fee. PLACEHOLDER for the fee step: it is the
    /// link to the payment, which will be held in the payment table ported from epr-payment-service.
    /// </summary>
    public string? PaymentReference { get; private set; }

    public DateTimeOffset? PaymentRecordedAt { get; private set; }

    // The submission to the regulator.

    public Guid? SubmittedFileId { get; private set; }

    public Guid? SubmittedByUserId { get; private set; }

    /// <summary>The name given on the declaration.</summary>
    public string? SubmittedBy { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    // The regulator's decision, which closes the cycle.

    public PackagingRegulatorDecision? Decision { get; private set; }

    public string? DecisionComments { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    internal void RecordPayment(string paymentReference, DateTimeOffset now)
    {
        PaymentReference = paymentReference;
        PaymentRecordedAt = now;
    }

    internal void Submit(Guid fileId, Guid submittedByUserId, string submittedBy, DateTimeOffset now)
    {
        SubmittedFileId = fileId;
        SubmittedByUserId = submittedByUserId;
        SubmittedBy = submittedBy;
        SubmittedAt = now;
    }

    internal void Decide(PackagingRegulatorDecision decision, string? comments, Guid decidedByUserId, DateTimeOffset now)
    {
        Decision = decision;
        DecisionComments = comments;
        DecidedByUserId = decidedByUserId;
        DecidedAt = now;
    }
}
