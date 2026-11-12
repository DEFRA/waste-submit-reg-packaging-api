namespace WasteSubmitRegPackaging.Registration.Domain;

/// <summary>
/// Mints the registration reference number on the first Grant. It is an interface so the aggregate
/// stays deterministic in tests. The number is stored on the submission and never re-minted, so
/// retrying a Grant can't produce a second number (rule R12), and a unique index backs that up.
/// </summary>
internal interface IRegistrationReferenceNumberGenerator
{
    string Generate(RegistrationSubmission submission);
}
