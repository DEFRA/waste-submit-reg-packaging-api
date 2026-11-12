using WasteSubmitRegPackaging.SharedKernel.Actors;
using WasteSubmitRegPackaging.SharedKernel.Domain;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Registration.Domain;

/// <summary>
/// One organisation's registration for one year, and the single owner of its state machine:
///
/// <code>
///   InProgress           --submit (producer)--&gt; SubmittedForApproval
///   SubmittedForApproval --Grant--&gt; Granted, --Refuse--&gt; Refused, --Query--&gt; Queried
///   Queried              --submit (producer)--&gt; SubmittedForApproval (the regulator can also decide again)
///   Granted              --Cancel--&gt; Cancelled
/// </code>
///
/// Today this logic is spread across the packaging frontend, SubAPI, Synapse procedures and the
/// regulator facade. Here every transition goes through a method that returns a <see cref="Result"/>,
/// and the allowed regulator actions are worked out once, in <see cref="AvailableRegulatorDecisions"/>,
/// for every UI to use.
///
/// SAMPLE: the real model adds cycles (one per upload, with files, producers and subsidiaries), fees,
/// payment method and resubmission (see the gap analysis).
/// </summary>
internal sealed class RegistrationSubmission : AggregateRoot<RegistrationSubmissionId>
{
    private readonly List<RegistrationSubmissionEvent> _history = [];

    private RegistrationSubmission()
    {
        // For EF Core.
    }

    public Guid OrganisationId { get; private set; }

    public Guid? ComplianceSchemeId { get; private set; }

    public int RegistrationYear { get; private set; }

    public RegistrationJourney Journey { get; private set; }

    public Nation Nation { get; private set; }

    public string ApplicationReferenceNumber { get; private set; } = null!;

    public string? RegistrationReferenceNumber { get; private set; }

    public RegistrationStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SubmittedForApprovalAt { get; private set; }

    public DateTimeOffset? LatestDecisionAt { get; private set; }

    /// <summary>The date of the first Grant (rule R3). Later decisions don't move it.</summary>
    public DateTimeOffset? RegistrationDate { get; private set; }

    public IReadOnlyList<RegistrationSubmissionEvent> History => _history;

    public static Result<RegistrationSubmission> Start(
        Actor producer,
        Guid organisationId,
        Guid? complianceSchemeId,
        int registrationYear,
        RegistrationJourney journey,
        Nation nation,
        string applicationReferenceNumber,
        DateTimeOffset now)
    {
        if (journey.IsComplianceScheme() != complianceSchemeId.HasValue)
            return RegistrationErrors.ComplianceSchemeMismatch;

        var submission = new RegistrationSubmission
        {
            Id = RegistrationSubmissionId.New(),
            OrganisationId = organisationId,
            ComplianceSchemeId = complianceSchemeId,
            RegistrationYear = registrationYear,
            Journey = journey,
            Nation = nation,
            ApplicationReferenceNumber = applicationReferenceNumber.Trim().ToUpperInvariant(),
            Status = RegistrationStatus.InProgress,
            CreatedByUserId = producer.UserId,
            CreatedAt = now
        };

        submission._history.Add(new RegistrationSubmissionEvent(RegistrationEventType.Started, producer, null, null, now));
        submission.Raise(new RegistrationStarted(submission.Id.Value, organisationId, registrationYear, now));

        return submission;
    }

    /// <summary>The producer submits the application to the regulator, with optional additional information.</summary>
    public Result SubmitApplication(Actor producer, string? comments, DateTimeOffset now)
    {
        if (Status is not (RegistrationStatus.InProgress or RegistrationStatus.Queried))
            return RegistrationErrors.SubmissionNotAllowed(Status);

        Status = RegistrationStatus.SubmittedForApproval;
        SubmittedForApprovalAt = now;

        _history.Add(new RegistrationSubmissionEvent(RegistrationEventType.ApplicationSubmitted, producer, comments, null, now));
        Raise(new RegistrationApplicationSubmitted(Id.Value, producer.UserId, now));

        return Result.Success();
    }

    /// <summary>
    /// The decisions a regulator can make now. It matches today's action buttons: Grant, Query and Refuse
    /// unless the registration is Granted, Refused or Cancelled, and Cancel only when Granted
    /// (<c>_RegistrationSubmissionActionButtons.cshtml</c>).
    /// </summary>
    public IReadOnlyList<RegulatorDecision> AvailableRegulatorDecisions() => Status switch
    {
        RegistrationStatus.SubmittedForApproval or RegistrationStatus.Queried =>
            [RegulatorDecision.Grant, RegulatorDecision.Refuse, RegulatorDecision.Query],
        RegistrationStatus.Granted => [RegulatorDecision.Cancel],
        _ => []
    };

    public Result RecordDecision(
        RegulatorDecision decision,
        Actor regulator,
        string? comments,
        DateTimeOffset? cancellationEffectiveDate,
        IRegistrationReferenceNumberGenerator referenceNumberGenerator,
        DateTimeOffset now)
    {
        if (!AvailableRegulatorDecisions().Contains(decision))
            return RegistrationErrors.DecisionNotAllowed(decision, Status);

        DateTimeOffset? effectiveDate = null;

        switch (decision)
        {
            case RegulatorDecision.Grant:
                Status = RegistrationStatus.Granted;
                RegistrationReferenceNumber ??= referenceNumberGenerator.Generate(this);
                RegistrationDate ??= now;
                break;
            case RegulatorDecision.Refuse:
                Status = RegistrationStatus.Refused;
                break;
            case RegulatorDecision.Query:
                Status = RegistrationStatus.Queried;
                break;
            case RegulatorDecision.Cancel:
                Status = RegistrationStatus.Cancelled;
                effectiveDate = cancellationEffectiveDate ?? now;
                break;
        }

        LatestDecisionAt = now;

        _history.Add(new RegistrationSubmissionEvent(ToEventType(decision), regulator, comments, effectiveDate, now));
        Raise(new RegistrationDecisionRecorded(
            Id.Value, decision, Status, RegistrationReferenceNumber, regulator.UserId, comments, now));

        return Result.Success();
    }

    private static RegistrationEventType ToEventType(RegulatorDecision decision) => decision switch
    {
        RegulatorDecision.Grant => RegistrationEventType.Granted,
        RegulatorDecision.Refuse => RegistrationEventType.Refused,
        RegulatorDecision.Query => RegistrationEventType.Queried,
        RegulatorDecision.Cancel => RegistrationEventType.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, null)
    };
}
