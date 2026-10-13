using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

[ExcludeFromCodeCoverage]
public class ApprovedSubmission
{
    public required Guid SubmitterId { get; init; }

    public required SubmitterType SubmitterType { get; init; }

    public required IReadOnlyList<OrganisationPackaging> AggregatedPackaging { get; init; }
}