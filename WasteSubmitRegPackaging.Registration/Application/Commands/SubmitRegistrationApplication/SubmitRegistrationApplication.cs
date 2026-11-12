using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Actors;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Registration.Application.Commands.SubmitRegistrationApplication;

/// <summary>The producer's final submit, with optional additional information for the regulator.</summary>
internal sealed record SubmitRegistrationApplicationCommand(
    Actor Actor,
    RegistrationSubmissionId RegistrationSubmissionId,
    string? Comments) : ICommand<Unit>;

internal sealed class SubmitRegistrationApplicationHandler(IRegistrationSubmissionRepository repository, TimeProvider timeProvider)
    : ICommandHandler<SubmitRegistrationApplicationCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(SubmitRegistrationApplicationCommand command, CancellationToken cancellationToken)
    {
        if (command.Actor.Type != ActorType.Producer)
            return RegistrationErrors.OnlyProducers;

        var submission = await repository.GetAsync(command.RegistrationSubmissionId, cancellationToken);
        if (submission is null)
            return RegistrationErrors.NotFound(command.RegistrationSubmissionId);

        if (command.Actor.OrganisationId != submission.OrganisationId)
            return RegistrationErrors.NotYourOrganisation;

        var submitted = submission.SubmitApplication(command.Actor, command.Comments, timeProvider.GetUtcNow());
        if (submitted.IsFailure)
            return submitted.Error;

        var saved = await repository.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Unit.Value : saved.Error;
    }
}
