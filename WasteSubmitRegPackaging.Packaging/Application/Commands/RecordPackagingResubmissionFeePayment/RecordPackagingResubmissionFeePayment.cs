using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;
using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackaging.Packaging.Application.Commands.RecordPackagingResubmissionFeePayment;

internal sealed record RecordPackagingResubmissionFeePaymentCommand(
    ProducerUser Producer, PackagingSubmissionId PackagingSubmissionId, string PaymentReference)
    : ICommand<PackagingSubmissionView>;

/// <summary>
/// PLACEHOLDER for the fee step. Resubmissions only: it records a payment reference on the cycle, which moves the
/// workflow on to submitting. It works out no fee and takes no payment.
///
/// What fills it in: the fee is worked out by the Fees module from what the file says (the regulator, the members),
/// and the payment is captured in the payment table ported from epr-payment-service, which this reference points to.
/// The step, and where it sits in the workflow, stay as they are. See docs/packaging_workflow.md.
/// </summary>
internal sealed class RecordPackagingResubmissionFeePaymentHandler(
    IPackagingSubmissionRepository repository, TimeProvider timeProvider)
    : ICommandHandler<RecordPackagingResubmissionFeePaymentCommand, PackagingSubmissionView>
{
    public async Task<Result<PackagingSubmissionView>> HandleAsync(
        RecordPackagingResubmissionFeePaymentCommand command, CancellationToken cancellationToken)
    {
        var submission = await repository.GetAsync(command.PackagingSubmissionId, cancellationToken);
        if (submission is null)
            return PackagingErrors.NotFound(command.PackagingSubmissionId);

        if (!command.Producer.ActsFor(submission.OrganisationId))
            return PackagingErrors.NotYourOrganisation;

        var recorded = submission.RecordResubmissionFeePayment(command.PaymentReference, timeProvider.GetUtcNow());
        if (recorded.IsFailure)
            return recorded.Error;

        var saved = await repository.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? PackagingSubmissionView.From(submission) : saved.Error;
    }
}
