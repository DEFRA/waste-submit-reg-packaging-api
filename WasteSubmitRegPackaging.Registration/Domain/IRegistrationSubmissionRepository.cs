using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Registration.Domain;

/// <summary>
/// Loads and saves the aggregate, and is used by command handlers only. Query handlers read the
/// DbContext directly, because they don't need the aggregate.
///
/// <see cref="SaveChangesAsync"/> returns a <see cref="Result"/>. A concurrent edit or a unique-key clash
/// becomes a Conflict, recognised from the Postgres error code rather than by matching message text.
/// </summary>
internal interface IRegistrationSubmissionRepository
{
    Task<RegistrationSubmission?> GetAsync(RegistrationSubmissionId id, CancellationToken cancellationToken);

    Task<bool> ApplicationReferenceNumberExistsAsync(string applicationReferenceNumber, CancellationToken cancellationToken);

    void Add(RegistrationSubmission submission);

    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
}
