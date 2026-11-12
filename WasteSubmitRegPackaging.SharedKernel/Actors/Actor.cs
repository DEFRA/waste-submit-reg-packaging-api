namespace WasteSubmitRegPackaging.SharedKernel.Actors;

public enum ActorType
{
    Producer,
    Regulator,
    System
}

/// <summary>
/// The person, or system, a request is made on behalf of. Service-to-service authentication tells us
/// which service is calling; the actor tells us which user of that service.
///
/// Commands carry the actor explicitly. That keeps handlers easy to test, and lets every recorded
/// event say who did it.
/// </summary>
public sealed record Actor(Guid UserId, Guid? OrganisationId, ActorType Type);

/// <summary>Resolves the actor of the current request. Null when the caller did not supply one.</summary>
public interface IActorContext
{
    Actor? Current { get; }
}
