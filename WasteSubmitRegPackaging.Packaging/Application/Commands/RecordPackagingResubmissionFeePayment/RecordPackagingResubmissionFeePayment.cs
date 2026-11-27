using WasteSubmitRegPackaging.Fees.Contracts;
using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;
using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackaging.Packaging.Application.Commands.RecordPackagingResubmissionFeePayment;

/// <param name="Regulator">SAMPLE: passed in, because the submission doesn't hold the submitter's nation yet.</param>
/// <param name="MemberCount">SAMPLE: passed in. Today it comes from epr-common-data-api. Ignored for a direct producer.</param>
/// <param name="PreviousPayments">SAMPLE: passed in, in pence. Payments stay in epr-payment-service until they move.</param>
internal sealed record RecordPackagingResubmissionFeePaymentCommand(
    ProducerUser Producer,
    PackagingSubmissionId PackagingSubmissionId,
    PaymentMethod PaymentMethod,
    RegulatorNation Regulator,
    int MemberCount,
    decimal PreviousPayments) : ICommand<PackagingSubmissionView>;

/// <summary>
/// Resubmissions only: the producer chooses how to pay, and the fee for this cycle is fixed at that moment. Fees works
/// the fee out (<see cref="IFeesApi"/>); this module stores it as the cycle's snapshot, in the same transaction.
/// See "Fees in a workflow" in docs/project_architecture.md. No payment is taken here.
///
/// To show the fee before this step, a screen calls <c>POST /fees/packaging-resubmission</c>.
/// </summary>
internal sealed class RecordPackagingResubmissionFeePaymentHandler(
    IPackagingSubmissionRepository repository,
    IFeesApi fees,
    TimeProvider timeProvider)
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

        // Before working the fee out: a fee for a first submission, or a second fee for a cycle, is a 409.
        if (submission.NextStep() != PackagingNextStep.PayResubmissionFee)
            return PackagingErrors.StepNotAvailable(PackagingNextStep.PayResubmissionFee, submission.NextStep());

        var now = timeProvider.GetUtcNow();

        var fee = await fees.CalculatePackagingResubmissionFeeAsync(
            new PackagingResubmissionFeeInput(
                submission.ComplianceSchemeId is null ? Submitter.Producer : Submitter.ComplianceScheme,
                command.Regulator,
                now,
                command.MemberCount,
                command.PreviousPayments),
            cancellationToken);
        if (fee.IsFailure)
            return fee.Error;

        var recorded = submission.RecordResubmissionFeePayment(command.PaymentMethod, fee.Value, now);
        if (recorded.IsFailure)
            return recorded.Error;

        var saved = await repository.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? PackagingSubmissionView.From(submission) : saved.Error;
    }
}
