using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.Packaging.Infrastructure;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Packaging.Application.Queries.GetPackagingSubmission;

internal sealed record GetPackagingSubmissionQuery(PackagingSubmissionId PackagingSubmissionId)
    : IQuery<PackagingSubmissionView>;

internal sealed record PackagingSubmissionView(
    Guid Id,
    Guid OrganisationId,
    int PackagingYear,
    Guid RegistrationSubmissionId,
    PackagingSubmissionStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>Projects straight from the table into the view in SQL, with no aggregate and no tracking.</summary>
internal sealed class GetPackagingSubmissionHandler(PackagingDbContext dbContext)
    : IQueryHandler<GetPackagingSubmissionQuery, PackagingSubmissionView>
{
    public async Task<Result<PackagingSubmissionView>> HandleAsync(
        GetPackagingSubmissionQuery query, CancellationToken cancellationToken)
    {
        var view = await dbContext.PackagingSubmissions
            .AsNoTracking()
            .Where(s => s.Id == query.PackagingSubmissionId)
            .Select(s => new PackagingSubmissionView(
                s.Id.Value, s.OrganisationId, s.PackagingYear, s.RegistrationSubmissionId, s.Status, s.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        return view is null ? PackagingErrors.NotFound(query.PackagingSubmissionId) : view;
    }
}
