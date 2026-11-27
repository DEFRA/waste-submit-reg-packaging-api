using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.Packaging.Infrastructure;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;
using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackaging.Packaging.Application.Queries.GetPackagingSubmission;

/// <summary>A submission by its id: what a page in the journey loads, and what the "uploading" page polls.</summary>
internal sealed record GetPackagingSubmissionQuery(PackagingSubmissionId PackagingSubmissionId, ProducerUser Producer)
    : IQuery<PackagingSubmissionView>;

/// <summary>
/// The submission a submitter has for a submission period: what a tile shows. <c>packaging.not_started</c> (404) when
/// there isn't one, which is the "not started" state.
/// </summary>
internal sealed record FindPackagingSubmissionQuery(
    ProducerUser Producer, Guid OrganisationId, Guid? ComplianceSchemeId, string SubmissionPeriod)
    : IQuery<PackagingSubmissionView>;

/// <summary>
/// Reads with no tracking, and skips the repository. It loads the aggregate, because the next step is a domain rule.
/// A list query would project straight to rows in SQL instead.
/// </summary>
internal sealed class GetPackagingSubmissionHandler(PackagingDbContext dbContext)
    : IQueryHandler<GetPackagingSubmissionQuery, PackagingSubmissionView>,
      IQueryHandler<FindPackagingSubmissionQuery, PackagingSubmissionView>
{
    public async Task<Result<PackagingSubmissionView>> HandleAsync(
        GetPackagingSubmissionQuery query, CancellationToken cancellationToken)
    {
        var submission = await dbContext.PackagingSubmissions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == query.PackagingSubmissionId, cancellationToken);

        if (submission is null)
            return PackagingErrors.NotFound(query.PackagingSubmissionId);

        if (!query.Producer.ActsFor(submission.OrganisationId))
            return PackagingErrors.NotYourOrganisation;

        return PackagingSubmissionView.From(submission);
    }

    public async Task<Result<PackagingSubmissionView>> HandleAsync(
        FindPackagingSubmissionQuery query, CancellationToken cancellationToken)
    {
        if (!query.Producer.ActsFor(query.OrganisationId))
            return PackagingErrors.NotYourOrganisation;

        var submission = await dbContext.PackagingSubmissions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                s => s.OrganisationId == query.OrganisationId
                     && s.ComplianceSchemeId == query.ComplianceSchemeId
                     && s.SubmissionPeriod == query.SubmissionPeriod,
                cancellationToken);

        return submission is null ? PackagingErrors.NotStarted : PackagingSubmissionView.From(submission);
    }
}
