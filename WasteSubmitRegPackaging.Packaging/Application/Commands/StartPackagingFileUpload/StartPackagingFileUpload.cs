using System.Security.Cryptography;
using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;
using WasteSubmitRegPackaging.SharedKernel.Users;
using WasteSubmitRegPackaging.SubmissionPeriods.Contracts;

namespace WasteSubmitRegPackaging.Packaging.Application.Commands.StartPackagingFileUpload;

/// <param name="SubmissionPeriod">The submission period's name, for example "July to December 2025".</param>
internal sealed record StartPackagingFileUploadCommand(
    ProducerUser Producer, Guid OrganisationId, Guid? ComplianceSchemeId, string SubmissionPeriod)
    : ICommand<StartPackagingFileUploadResult>;

/// <param name="CallbackToken">The secret that goes in the callback URL given to the uploader.</param>
internal sealed record StartPackagingFileUploadResult(Guid PackagingSubmissionId, Guid FileId, string CallbackToken);

/// <summary>
/// The start of an upload, and the only place a packaging submission is created: the first upload for a submitter
/// and period creates it, and every later one finds it. Nothing is recorded before this (the tile and "before you
/// start" are navigation).
///
/// The caller then starts the upload with the CDP uploader, giving it the callback URL for this file; the uploader
/// reports the result to <c>RecordPackagingFileScanResult</c>.
/// </summary>
internal sealed class StartPackagingFileUploadHandler(
    IPackagingSubmissionRepository repository,
    ISubmissionPeriodsApi submissionPeriods,
    TimeProvider timeProvider)
    : ICommandHandler<StartPackagingFileUploadCommand, StartPackagingFileUploadResult>
{
    public async Task<Result<StartPackagingFileUploadResult>> HandleAsync(
        StartPackagingFileUploadCommand command, CancellationToken cancellationToken)
    {
        if (!command.Producer.ActsFor(command.OrganisationId))
            return PackagingErrors.NotYourOrganisation;

        var now = timeProvider.GetUtcNow();

        var submission = await repository.FindAsync(
            command.OrganisationId, command.ComplianceSchemeId, command.SubmissionPeriod, cancellationToken);

        if (submission is null)
        {
            var period = (await submissionPeriods.GetPackagingSubmissionPeriodsAsync(cancellationToken))
                .SingleOrDefault(p => p.Name == command.SubmissionPeriod);
            if (period is null)
                return PackagingErrors.UnknownSubmissionPeriod(command.SubmissionPeriod);

            // Still to add: is the period open for this submitter (ISubmissionPeriodsApi), and does the user act
            // for the compliance scheme.

            submission = PackagingSubmission.Start(
                command.Producer, command.OrganisationId, command.ComplianceSchemeId,
                period.Name, period.Year, period.RegistrationYear, now);
            repository.Add(submission);
        }

        var started = submission.StartFileUpload(command.Producer, NewCallbackToken(), now);
        if (started.IsFailure)
            return started.Error;

        var saved = await repository.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess
            ? new StartPackagingFileUploadResult(submission.Id.Value, started.Value.Id, started.Value.CallbackToken)
            : saved.Error;
    }

    private static string NewCallbackToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
}
