namespace WasteSubmitRegPackaging.Registration.Contracts;

/// <summary>
/// The in-process API other modules use to ask the Registration module questions. It is a normal
/// interface, so if Registration is ever split into its own service this becomes an HTTP client and
/// callers don't change.
/// </summary>
public interface IRegistrationModuleApi
{
    /// <summary>The organisation's latest registration for the year, or null if it has none.</summary>
    Task<RegistrationSummary?> GetLatestRegistrationAsync(
        Guid organisationId, int registrationYear, CancellationToken cancellationToken);
}

/// <summary>
/// The contract's own copy of the registration state. It is deliberately separate from the domain
/// enum, so Registration can change its internals without breaking other modules.
/// </summary>
public enum RegistrationState
{
    InProgress,
    SubmittedForApproval,
    Queried,
    Granted,
    Refused,
    Cancelled
}

public sealed record RegistrationSummary(
    Guid RegistrationSubmissionId,
    int RegistrationYear,
    RegistrationState State);
