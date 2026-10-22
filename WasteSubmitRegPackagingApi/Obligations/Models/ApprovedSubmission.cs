using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

[ExcludeFromCodeCoverage]
public class ApprovedSubmission
{
    [Description("The submitter: a direct registrant's organisation ID, or a compliance scheme's ID. A scheme's ID is not the organisation ID of the company that runs it.")]
    public required Guid SubmitterOrganisationId { get; init; }

    [Description("Whether the submitter is a direct registrant or a compliance scheme.")]
    public required SubmitterType SubmitterType { get; init; }

    [Description("The producers the submitter reported for: itself for a direct registrant, its members for a compliance scheme.")]
    public required IReadOnlyList<ProducerPackaging> Producers { get; init; }
}