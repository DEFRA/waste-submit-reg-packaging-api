namespace WasteSubmitRegPackaging.SharedKernel.Domain;

/// <summary>
/// Something with an identity. Ids are strongly typed value types (for example
/// <c>RegistrationSubmissionId</c>), generated in code with <see cref="Guid.CreateVersion7()"/> and never
/// by the database. That way they are known before saving and sort roughly by creation time.
/// </summary>
public abstract class Entity<TId>
    where TId : struct
{
    public TId Id { get; protected init; }
}
