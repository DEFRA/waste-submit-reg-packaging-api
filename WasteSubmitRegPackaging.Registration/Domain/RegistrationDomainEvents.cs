using WasteSubmitRegPackaging.SharedKernel.Domain;

namespace WasteSubmitRegPackaging.Registration.Domain;

// Raised by the aggregate and saved to reg_outbox_message in the same transaction. The outbox
// dispatcher then sends them on, for example as GOV.UK Notify emails, or as a feed for the public
// register.

internal sealed record RegistrationStarted(
    Guid RegistrationSubmissionId,
    Guid OrganisationId,
    int RegistrationYear,
    DateTimeOffset OccurredAt) : IDomainEvent;

internal sealed record RegistrationApplicationSubmitted(
    Guid RegistrationSubmissionId,
    Guid SubmittedByUserId,
    DateTimeOffset OccurredAt) : IDomainEvent;

internal sealed record RegistrationDecisionRecorded(
    Guid RegistrationSubmissionId,
    RegulatorDecision Decision,
    RegistrationStatus Status,
    string? RegistrationReferenceNumber,
    Guid RegulatorUserId,
    string? Comments,
    DateTimeOffset OccurredAt) : IDomainEvent;
