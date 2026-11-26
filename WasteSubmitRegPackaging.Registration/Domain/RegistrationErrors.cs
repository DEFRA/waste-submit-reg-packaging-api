using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Registration.Domain;

/// <summary>
/// Every error the module can return, in one place, with stable codes. The codes are part of the API
/// contract: frontends may switch on them.
/// </summary>
internal static class RegistrationErrors
{
    public static Error NotFound(RegistrationSubmissionId id) =>
        Error.NotFound("registration.not_found", $"No registration submission with ID {id}.");

    public static readonly Error ApplicationReferenceNumberInUse =
        Error.Conflict("registration.application_reference_in_use", "The application reference number is already in use.");

    public static readonly Error ComplianceSchemeMismatch =
        Error.Validation(
            "registration.compliance_scheme_mismatch",
            "A compliance scheme ID is required for compliance scheme journeys and not allowed for direct producers.");

    public static readonly Error NotYourOrganisation =
        Error.Forbidden("registration.not_your_organisation", "The user does not act for this organisation.");

    public static readonly Error CommentsRequired =
        Error.Validation("registration.comments_required", "Comments are required when querying a registration.");

    public static Error SubmissionNotAllowed(RegistrationStatus status) =>
        Error.Conflict("registration.submission_not_allowed", $"An application cannot be submitted while the registration is {status}.");

    public static Error DecisionNotAllowed(RegulatorDecision decision, RegistrationStatus status) =>
        Error.Conflict("registration.decision_not_allowed", $"{decision} is not allowed while the registration is {status}.");

    public static readonly Error ConcurrentChange =
        Error.Conflict("registration.concurrent_change", "Someone else changed this registration. Reload it and try again.");
}
