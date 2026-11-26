using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackaging.Registration.Domain;

/// <summary>
/// One entry in a registration's append-only history (<c>reg_registration_submission_event</c>). Rows
/// are never updated. The current state lives on the aggregate, so reads don't replay events. That is
/// the change from today's Cosmos and Synapse model, where every read recalculates status from events.
/// </summary>
internal sealed class RegistrationSubmissionEvent
{
    private RegistrationSubmissionEvent()
    {
        // For EF Core.
    }

    private RegistrationSubmissionEvent(
        RegistrationEventType eventType,
        Guid userId,
        UserType userType,
        string? comments,
        DateTimeOffset? effectiveDate,
        DateTimeOffset occurredAt)
    {
        Id = Guid.CreateVersion7();
        EventType = eventType;
        UserId = userId;
        UserType = userType;
        Comments = comments;
        EffectiveDate = effectiveDate;
        OccurredAt = occurredAt;
    }

    public static RegistrationSubmissionEvent ByProducer(
        RegistrationEventType eventType, ProducerUser user, string? comments, DateTimeOffset? effectiveDate, DateTimeOffset occurredAt) =>
        new(eventType, user.UserId, UserType.Producer, comments, effectiveDate, occurredAt);

    public static RegistrationSubmissionEvent ByRegulator(
        RegistrationEventType eventType, RegulatorUser user, string? comments, DateTimeOffset? effectiveDate, DateTimeOffset occurredAt) =>
        new(eventType, user.UserId, UserType.Regulator, comments, effectiveDate, occurredAt);

    public Guid Id { get; private init; }

    public RegistrationEventType EventType { get; private init; }

    /// <summary>Who did it.</summary>
    public Guid UserId { get; private init; }

    public UserType UserType { get; private init; }

    /// <summary>The producer's additional information, or the regulator's decision comments.</summary>
    public string? Comments { get; private init; }

    /// <summary>When a cancellation takes effect. It can differ from when it was recorded.</summary>
    public DateTimeOffset? EffectiveDate { get; private init; }

    public DateTimeOffset OccurredAt { get; private init; }
}
