using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.Registration.Contracts;
using WasteSubmitRegPackaging.SharedKernel.Actors;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Packaging.Application.Commands.CreatePackagingSubmission;

internal sealed record CreatePackagingSubmissionCommand(Actor Actor, Guid OrganisationId, int PackagingYear)
    : ICommand<PackagingSubmissionId>;

/// <summary>
/// Shows one module asking another a question. Packaging checks registration through
/// <see cref="IRegistrationModuleApi"/>, never by reading <c>reg_*</c> tables.
/// </summary>
internal sealed class CreatePackagingSubmissionHandler(
    IPackagingSubmissionRepository repository,
    IRegistrationModuleApi registrations,
    TimeProvider timeProvider)
    : ICommandHandler<CreatePackagingSubmissionCommand, PackagingSubmissionId>
{
    public async Task<Result<PackagingSubmissionId>> HandleAsync(
        CreatePackagingSubmissionCommand command, CancellationToken cancellationToken)
    {
        if (command.Actor.Type != ActorType.Producer)
            return PackagingErrors.OnlyProducers;

        if (command.Actor.OrganisationId != command.OrganisationId)
            return PackagingErrors.NotYourOrganisation;

        // SAMPLE rule, to confirm with the business: which registration year a packaging year needs.
        // The same year is used here only to show the call between modules.
        var registration = await registrations.GetLatestRegistrationAsync(
            command.OrganisationId, command.PackagingYear, cancellationToken);

        if (registration is not { State: RegistrationState.Granted })
            return PackagingErrors.NotRegistered;

        var submission = PackagingSubmission.Create(
            command.Actor, command.OrganisationId, command.PackagingYear, registration.RegistrationSubmissionId, timeProvider.GetUtcNow());

        repository.Add(submission);
        await repository.SaveChangesAsync(cancellationToken);

        return submission.Id;
    }
}
