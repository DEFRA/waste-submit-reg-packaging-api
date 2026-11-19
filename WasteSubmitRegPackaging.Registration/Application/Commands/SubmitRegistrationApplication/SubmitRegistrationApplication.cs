using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Users;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Registration.Application.Commands.SubmitRegistrationApplication;

/// <summary>The producer's final submit, with optional additional information for the regulator.</summary>
internal sealed record SubmitRegistrationApplicationCommand(
    ProducerUser Producer,
    RegistrationSubmissionId RegistrationSubmissionId,
    string? Comments) : ICommand<Unit>;

internal sealed class SubmitRegistrationApplicationHandler(IRegistrationSubmissionRepository repository, TimeProvider timeProvider)
    : ICommandHandler<SubmitRegistrationApplicationCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(SubmitRegistrationApplicationCommand command, CancellationToken cancellationToken)
    {
        var submission = await repository.GetAsync(command.RegistrationSubmissionId, cancellationToken);
        if (submission is null)
            return RegistrationErrors.NotFound(command.RegistrationSubmissionId);

        if (!command.Producer.ActsFor(submission.OrganisationId))
            return RegistrationErrors.NotYourOrganisation;

        var submitted = submission.SubmitApplication(command.Producer, command.Comments, timeProvider.GetUtcNow());
        if (submitted.IsFailure)
            return submitted.Error;

        var saved = await repository.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Unit.Value : saved.Error;
    }
}
