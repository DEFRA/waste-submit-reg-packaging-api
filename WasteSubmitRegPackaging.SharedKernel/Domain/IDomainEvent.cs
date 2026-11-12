namespace WasteSubmitRegPackaging.SharedKernel.Domain;

/// <summary>
/// Something that happened inside a module, raised by an aggregate. Domain events are written to the
/// module's outbox table in the same transaction as the change, then dispatched afterwards to
/// notifications, other modules or downstream systems.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
