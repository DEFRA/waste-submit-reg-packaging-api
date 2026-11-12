using WasteSubmitRegPackaging.SharedKernel.Actors;

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

    public RegistrationSubmissionEvent(
        RegistrationEventType eventType, Actor actor, string? comments, DateTimeOffset? effectiveDate, DateTimeOffset occurredAt)
    {
        Id = Guid.CreateVersion7();
        EventType = eventType;
        ActorUserId = actor.UserId;
        ActorType = actor.Type;
        Comments = comments;
        EffectiveDate = effectiveDate;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private init; }

    public RegistrationEventType EventType { get; private init; }

    public Guid ActorUserId { get; private init; }

    public ActorType ActorType { get; private init; }

    /// <summary>The producer's additional information, or the regulator's decision comments.</summary>
    public string? Comments { get; private init; }

    /// <summary>When a cancellation takes effect. It can differ from when it was recorded.</summary>
    public DateTimeOffset? EffectiveDate { get; private init; }

    public DateTimeOffset OccurredAt { get; private init; }
}
