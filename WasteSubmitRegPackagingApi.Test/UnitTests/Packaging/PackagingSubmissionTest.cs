using WasteSubmitRegPackaging.Packaging.Domain;

namespace WasteSubmitRegPackagingApi.Test.UnitTests.Packaging;

/// <summary>The packaging workflow's rules: the status, the cycles, and the next step at every point.</summary>
public class PackagingSubmissionTest
{
    private static readonly StoredFile s_stored = new("packaging.csv", "bucket", "upload/file");
    private static readonly PackagingFileCheck s_withErrors = new(ErrorCount: 3, WarningCount: 0);
    private static readonly PackagingFileCheck s_withWarnings = new(ErrorCount: 0, WarningCount: 2);

    private DateTimeOffset _now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_submission_starts_in_progress_with_cycle_1_waiting_for_the_file_check()
    {
        var submission = Started(out var file);

        Assert.Equal(PackagingSubmissionStatus.InProgress, submission.Status);
        Assert.Equal(1, submission.CurrentCycle.CycleNumber);
        Assert.False(submission.CurrentCycle.IsResubmission);
        Assert.Equal(PackagingFileStatus.Uploading, file.Status);
        Assert.Equal(PackagingNextStep.AwaitFileCheck, submission.NextStep());
        Assert.IsType<PackagingSubmissionStarted>(Assert.Single(submission.DomainEvents));
    }

    [Fact]
    public void A_file_the_uploader_rejects_means_upload_again()
    {
        var submission = Started(out var file);

        submission.RecordFileRejected(file, "packaging.csv", "The selected file contains a virus", Later());

        Assert.Equal(PackagingFileStatus.Rejected, file.Status);
        Assert.Null(file.S3Key);
        Assert.Equal(PackagingNextStep.UploadFile, submission.NextStep());
    }

    [Fact]
    public void A_stored_file_waits_for_validation_and_the_producer_still_waits()
    {
        var submission = Started(out var file);

        submission.RecordFileStored(file, s_stored, Later());

        Assert.Equal(PackagingFileStatus.Validating, file.Status);
        Assert.Equal(("packaging.csv", "bucket", "upload/file"), (file.FileName, file.S3Bucket, file.S3Key));
        Assert.Null(file.ErrorCount);
        Assert.Equal(PackagingNextStep.AwaitFileCheck, submission.NextStep());
        Assert.Equal(
            "packaging.step_not_available",
            submission.SubmitToRegulator(TestUsers.Producer, file.Id, "A Producer", Later()).Error?.Code);
    }

    [Fact]
    public void Only_a_file_that_is_waiting_for_validation_can_be_validated()
    {
        var submission = Started(out var file);

        var beforeItIsStored = submission.RecordFileValidated(file, PackagingFileCheck.Passed, Later());
        submission.RecordFileStored(file, s_stored, Later());
        var first = submission.RecordFileValidated(file, s_withErrors, Later());
        var second = submission.RecordFileValidated(file, PackagingFileCheck.Passed, Later());

        Assert.Equal("packaging.file_not_awaiting_validation", beforeItIsStored.Error?.Code);
        Assert.True(first.IsSuccess);
        Assert.Equal("packaging.file_not_awaiting_validation", second.Error?.Code);
        Assert.Equal(PackagingFileStatus.Invalid, file.Status);
    }

    [Fact]
    public void A_file_with_errors_has_to_be_fixed_and_cannot_be_submitted()
    {
        var submission = Started(out var file);
        Validated(submission, file, s_withErrors);

        var submitted = submission.SubmitToRegulator(TestUsers.Producer, file.Id, "A Producer", Later());

        Assert.Equal(PackagingFileStatus.Invalid, file.Status);
        Assert.Equal(3, file.ErrorCount);
        Assert.Equal(PackagingNextStep.FixErrors, submission.NextStep());
        Assert.Equal("packaging.step_not_available", submitted.Error?.Code);
    }

    [Fact]
    public void A_file_with_only_warnings_is_valid()
    {
        var submission = Started(out var file);

        Validated(submission, file, s_withWarnings);

        Assert.Equal(PackagingFileStatus.Valid, file.Status);
        Assert.Equal(2, file.WarningCount);
        Assert.Equal(PackagingNextStep.SubmitToRegulator, submission.NextStep());
    }

    [Fact]
    public void A_first_submission_goes_from_a_valid_file_straight_to_the_regulator_with_no_fee()
    {
        var submission = Started(out var file);
        Validated(submission, file, PackagingFileCheck.Passed);

        var submitted = submission.SubmitToRegulator(TestUsers.Producer, file.Id, "A Producer", Later());

        Assert.True(submitted.IsSuccess, submitted.Error?.Code);
        Assert.Equal(PackagingSubmissionStatus.SubmittedToRegulator, submission.Status);
        Assert.Equal(file.Id, submission.CurrentCycle.SubmittedFileId);
        Assert.Equal("A Producer", submission.CurrentCycle.SubmittedBy);
        Assert.Null(submission.NextStep());
    }

    [Fact]
    public void Only_the_latest_file_can_be_submitted()
    {
        var submission = Started(out var first);
        Validated(submission, first, PackagingFileCheck.Passed);
        var second = submission.StartFileUpload(TestUsers.Producer, "token-2", Later()).Value;
        Validated(submission, second, PackagingFileCheck.Passed);

        var submitted = submission.SubmitToRegulator(TestUsers.Producer, first.Id, "A Producer", Later());

        Assert.Equal("packaging.file_not_current", submitted.Error?.Code);
        Assert.Equal(1, submission.CurrentCycle.CycleNumber);
    }

    [Fact]
    public void A_file_cannot_be_uploaded_while_the_regulator_is_deciding()
    {
        var submission = Submitted();

        var upload = submission.StartFileUpload(TestUsers.Producer, "token-2", Later());

        Assert.Equal("packaging.upload_not_allowed", upload.Error?.Code);
    }

    [Theory]
    [InlineData(PackagingRegulatorDecision.Accepted, "AcceptedByRegulator")]
    [InlineData(PackagingRegulatorDecision.Rejected, "RejectedByRegulator")]
    public void A_decision_closes_the_cycle(PackagingRegulatorDecision decision, string expected)
    {
        var submission = Submitted();

        var decided = submission.RecordRegulatorDecision(decision, TestUsers.Regulator, "Comments", Later());

        Assert.True(decided.IsSuccess, decided.Error?.Code);
        Assert.Equal(expected, submission.Status.ToString());
        Assert.Equal(decision, submission.CurrentCycle.Decision);
        Assert.Null(submission.NextStep());
    }

    [Fact]
    public void A_decision_is_only_allowed_on_what_has_been_submitted()
    {
        var submission = Started(out _);

        var decided = submission.RecordRegulatorDecision(PackagingRegulatorDecision.Accepted, TestUsers.Regulator, null, Later());

        Assert.Equal("packaging.decision_not_allowed", decided.Error?.Code);
    }

    [Fact]
    public void An_upload_after_a_rejection_opens_a_new_cycle_that_is_not_a_resubmission()
    {
        var submission = Decided(PackagingRegulatorDecision.Rejected);

        var file = submission.StartFileUpload(TestUsers.Producer, "token-2", Later()).Value;
        Validated(submission, file, PackagingFileCheck.Passed);

        Assert.Equal(PackagingSubmissionStatus.InProgress, submission.Status);
        Assert.Equal(2, submission.CurrentCycle.CycleNumber);
        Assert.False(submission.CurrentCycle.IsResubmission);
        Assert.Equal(PackagingNextStep.SubmitToRegulator, submission.NextStep());
    }

    [Fact]
    public void An_upload_after_an_acceptance_opens_a_resubmission_whose_fee_step_comes_before_submitting()
    {
        var submission = Decided(PackagingRegulatorDecision.Accepted);
        var file = submission.StartFileUpload(TestUsers.Producer, "token-2", Later()).Value;
        Validated(submission, file, PackagingFileCheck.Passed);

        Assert.True(submission.CurrentCycle.IsResubmission);
        Assert.Equal(PackagingNextStep.PayResubmissionFee, submission.NextStep());
        Assert.Equal(
            "packaging.step_not_available",
            submission.SubmitToRegulator(TestUsers.Producer, file.Id, "A Producer", Later()).Error?.Code);

        var paid = submission.RecordResubmissionFeePayment("PAY-123", Later());

        Assert.True(paid.IsSuccess, paid.Error?.Code);
        Assert.Equal("PAY-123", submission.CurrentCycle.PaymentReference);
        Assert.Equal(PackagingNextStep.SubmitToRegulator, submission.NextStep());

        // The first cycle is untouched: a resubmission never overwrites what went before.
        Assert.Equal(PackagingRegulatorDecision.Accepted, submission.Cycles.Single(c => c.CycleNumber == 1).Decision);
    }

    [Fact]
    public void A_payment_cannot_be_recorded_on_a_first_submission_or_twice_for_one_cycle()
    {
        var first = Started(out var file);
        Validated(first, file, PackagingFileCheck.Passed);

        Assert.Equal("packaging.step_not_available", first.RecordResubmissionFeePayment("PAY-1", Later()).Error?.Code);

        var resubmission = Decided(PackagingRegulatorDecision.Accepted);
        var second = resubmission.StartFileUpload(TestUsers.Producer, "token-2", Later()).Value;
        Validated(resubmission, second, PackagingFileCheck.Passed);
        resubmission.RecordResubmissionFeePayment("PAY-2", Later());

        Assert.Equal("packaging.step_not_available", resubmission.RecordResubmissionFeePayment("PAY-3", Later()).Error?.Code);
        Assert.Equal("PAY-2", resubmission.CurrentCycle.PaymentReference);
    }

    private PackagingSubmission Started(out SubmissionFile file)
    {
        var submission = PackagingSubmission.Start(
            TestUsers.Producer, TestUsers.ProducerOrganisationId, complianceSchemeId: null,
            "July to December 2025", packagingYear: 2025, registrationYear: 2026, _now);

        file = submission.StartFileUpload(TestUsers.Producer, "token-1", _now).Value;
        return submission;
    }

    /// <summary>The uploader stores the file, then it is validated: two steps.</summary>
    private void Validated(PackagingSubmission submission, SubmissionFile file, PackagingFileCheck check)
    {
        submission.RecordFileStored(file, s_stored, Later());
        Assert.True(submission.RecordFileValidated(file, check, Later()).IsSuccess);
    }

    private PackagingSubmission Submitted()
    {
        var submission = Started(out var file);
        Validated(submission, file, PackagingFileCheck.Passed);
        Assert.True(submission.SubmitToRegulator(TestUsers.Producer, file.Id, "A Producer", Later()).IsSuccess);
        return submission;
    }

    private PackagingSubmission Decided(PackagingRegulatorDecision decision)
    {
        var submission = Submitted();
        Assert.True(submission.RecordRegulatorDecision(decision, TestUsers.Regulator, null, Later()).IsSuccess);
        return submission;
    }

    /// <summary>Each step happens a minute after the last, so "the latest file" is never a tie.</summary>
    private DateTimeOffset Later() => _now = _now.AddMinutes(1);
}
