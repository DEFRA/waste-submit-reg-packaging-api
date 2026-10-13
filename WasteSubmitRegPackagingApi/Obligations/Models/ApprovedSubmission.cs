using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

[ExcludeFromCodeCoverage]
public class ApprovedSubmission
{
    [Description("The organisation that submitted: a direct registrant or a compliance scheme.")]
    public required Guid SubmitterId { get; init; }

    [Description("Whether the submitter is a direct registrant or a compliance scheme.")]
    public required SubmitterType SubmitterType { get; init; }

    [Description("The organisations the submitter reported for: itself for a direct registrant, its members for a compliance scheme.")]
    public required IReadOnlyList<OrganisationPackaging> AggregatedPackaging { get; init; }
}