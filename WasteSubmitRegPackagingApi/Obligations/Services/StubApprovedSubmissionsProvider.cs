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
        var submission = ApprovedSubmissionsFixtures.ForYear(year)?
            .FirstOrDefault(approvedSubmission => approvedSubmission.SubmitterId == organisationId);

        var response = submission is null
            ? null
            : new ItemsResponse<OrganisationPackaging> { Items = submission.AggregatedPackaging };

        return Task.FromResult(response);
    }

    public Task<ApprovedSubmissionsResponse?> GetAggregatedSubmissionsAsync(
        int year,
        CancellationToken cancellationToken)
    {
        var submissions = ApprovedSubmissionsFixtures.ForYear(year);

        var response = submissions is null
            ? null
            : new ApprovedSubmissionsResponse { ApprovedSubmissions = submissions };

        return Task.FromResult(response);
    }
}