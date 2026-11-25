using System.ComponentModel.DataAnnotations;
using WasteSubmitRegPackaging.Registration.Domain;

namespace WasteSubmitRegPackaging.Registration.Endpoints;

// Request shapes. These are the API contract, kept separate from commands so that the API and the
// application can change independently. DataAnnotations here are checked by .NET 10's built-in
// minimal API validation (AddValidation), which returns a 400 ValidationProblem before the endpoint
// runs. Only shape rules go here; business rules live in handlers and the domain.
//
// Request types MUST be public, along with any enums they use. The validation source generator
// silently skips internal types, and the request then reaches the handler unvalidated. The
// architecture test checks this.
//
// Value types are nullable with [Required], so a missing field is a 400 rather than a silent default.
// Enums also have [EnumDataType]: without it, a number that isn't one of the enum's values ("nation": 9) gets through.

public sealed class StartRegistrationRequest
{
    [Required]
    public Guid? OrganisationId { get; init; }

    /// <summary>Required for compliance scheme journeys. The domain checks this.</summary>
    public Guid? ComplianceSchemeId { get; init; }

    [Required, Range(2025, 2100)]
    public int? RegistrationYear { get; init; }

    [Required, EnumDataType(typeof(RegistrationJourney))]
    public RegistrationJourney? Journey { get; init; }

    [Required, EnumDataType(typeof(Nation))]
    public Nation? Nation { get; init; }

    [Required, StringLength(50, MinimumLength = 5)]
    public string? ApplicationReferenceNumber { get; init; }
}

internal sealed record StartRegistrationResponse(Guid RegistrationSubmissionId);

public sealed class SubmitRegistrationApplicationRequest
{
    /// <summary>The producer's additional information. Today's UI limits it to 400 characters.</summary>
    [StringLength(400)]
    public string? Comments { get; init; }
}

public sealed class RecordRegulatorDecisionRequest
{
    [Required, EnumDataType(typeof(RegulatorDecision))]
    public RegulatorDecision? Decision { get; init; }

    [StringLength(400)]
    public string? Comments { get; init; }

    /// <summary>Cancel only. Defaults to now.</summary>
    public DateTimeOffset? CancellationEffectiveDate { get; init; }
}
