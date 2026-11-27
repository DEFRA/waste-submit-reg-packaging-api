using WasteSubmitRegPackaging.Fees.Contracts;

namespace WasteSubmitRegPackaging.Packaging.Domain;

/// <summary>
/// One pass from upload to the regulator's decision (<c>pkg_submission_cycle</c>). Cycle 1 is the first submission.
/// Each later upload after a decision opens the next cycle, so a resubmission never overwrites what went before.
/// </summary>
internal sealed class SubmissionCycle
{
    private readonly List<SubmissionFeeLine> _feeLines = [];

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

    // The fee, snapshotted when the producer chooses how to pay, and never recalculated. The header is one-to-one
    // with the cycle, so it is columns here; only the lines need rows (pkg_submission_fee_line).

    public PaymentMethod? PaymentMethod { get; private set; }

    /// <summary>In pence.</summary>
    public decimal? FeeTotal { get; private set; }

    public int? FeeMemberCount { get; private set; }

    public DateTimeOffset? FeeRecordedAt { get; private set; }

    public IReadOnlyList<SubmissionFeeLine> FeeLines => _feeLines;

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

    internal void RecordFeePayment(PaymentMethod paymentMethod, ResubmissionFee fee, DateTimeOffset now)
    {
        PaymentMethod = paymentMethod;
        FeeTotal = fee.TotalResubmissionFee;
        FeeMemberCount = fee.MemberCount;
        FeeRecordedAt = now;
        _feeLines.AddRange(fee.Lines.Select(line => new SubmissionFeeLine(line)));
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

/// <summary>
/// One charged item, in pence (<c>pkg_submission_fee_line</c>). It copies the unit price and description, so a later
/// rate change can't alter what was charged, and keeps the ids of the rate and fee type it came from.
/// </summary>
internal sealed class SubmissionFeeLine
{
    private SubmissionFeeLine()
    {
        // For EF Core.
    }

    internal SubmissionFeeLine(FeeLine line)
    {
        Id = Guid.CreateVersion7();
        FeeRateId = line.FeeRateId;
        FeeGroupId = line.FeeGroupId;
        Description = line.Description;
        UnitPrice = line.UnitPrice;
        Quantity = line.Quantity;
        Amount = line.Amount;
    }

    public Guid Id { get; private init; }

    public int FeeRateId { get; private init; }

    public int FeeGroupId { get; private init; }

    public string Description { get; private init; } = null!;

    public decimal UnitPrice { get; private init; }

    public int Quantity { get; private init; }

    public decimal Amount { get; private init; }
}
