using WasteSubmitRegPackagingApi.Obligations.Models;

namespace WasteSubmitRegPackagingApi.Obligations.Services;

/// <summary>
/// Serves example responses until the real data source is wired in. The filtered endpoint
/// matches on the submitter's organisation ID (a direct registrant or a compliance scheme).
/// </summary>
public class StubApprovedSubmissionsProvider : IApprovedSubmissionsProvider
{
    public Task<ItemsResponse<OrganisationPackaging>?> GetAggregatedSubmissionAsync(
        int year,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        if (!ApprovedSubmissionsFixtures.KnownSubmitterIds.Contains(organisationId))
        {
            return Task.FromResult<ItemsResponse<OrganisationPackaging>?>(null);
        }

        var submission = ApprovedSubmissionsFixtures.ForYear(year)
            .FirstOrDefault(approvedSubmission => approvedSubmission.SubmitterId == organisationId);

        ItemsResponse<OrganisationPackaging>? response =
            new ItemsResponse<OrganisationPackaging> { Items = submission?.AggregatedPackaging ?? [] };

        return Task.FromResult(response);
    }

    public Task<ItemsResponse<ApprovedSubmission>> GetAggregatedSubmissionsAsync(
        int year,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new ItemsResponse<ApprovedSubmission> { Items = ApprovedSubmissionsFixtures.ForYear(year) });
    }
}