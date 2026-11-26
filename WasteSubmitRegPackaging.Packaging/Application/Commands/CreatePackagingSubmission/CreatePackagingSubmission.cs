using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.SharedKernel.Users;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Packaging.Application.Commands.CreatePackagingSubmission;

internal sealed record CreatePackagingSubmissionCommand(ProducerUser Producer, Guid OrganisationId, int PackagingYear)
    : ICommand<PackagingSubmissionId>;

/// <summary>
/// Starts a draft packaging submission for the producer's organisation and year. SAMPLE: there's no registration
/// check, because producers report packaging data before they register; the submission records the registration year
/// it's for instead.
/// </summary>
internal sealed class CreatePackagingSubmissionHandler(IPackagingSubmissionRepository repository, TimeProvider timeProvider)
    : ICommandHandler<CreatePackagingSubmissionCommand, PackagingSubmissionId>
{
    public async Task<Result<PackagingSubmissionId>> HandleAsync(
        CreatePackagingSubmissionCommand command, CancellationToken cancellationToken)
    {
        if (!command.Producer.ActsFor(command.OrganisationId))
            return PackagingErrors.NotYourOrganisation;

        var submission = PackagingSubmission.Create(
            command.Producer, command.OrganisationId, command.PackagingYear, timeProvider.GetUtcNow());

        repository.Add(submission);
        await repository.SaveChangesAsync(cancellationToken);

        return submission.Id;
    }
}
