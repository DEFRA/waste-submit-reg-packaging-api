using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackagingApi.Test.UnitTests.Registration;

/// <summary>Domain tests: plain objects and no mocks. This is where most business-rule tests belong.</summary>
public class RegistrationSubmissionTest
{
    private static readonly DateTimeOffset s_now = RegistrationTestData.Now;

    [Fact]
    public void A_new_registration_is_in_progress_with_a_normalised_reference_and_a_started_event()
    {
        var submission = RegistrationTestData.Started();

        Assert.Equal(RegistrationStatus.InProgress, submission.Status);
        Assert.Equal("PEPR00012345", submission.ApplicationReferenceNumber);
        Assert.Equal(RegistrationEventType.Started, Assert.Single(submission.History).EventType);
        Assert.IsType<RegistrationStarted>(Assert.Single(submission.DomainEvents));
    }

    [Theory]
    [InlineData(nameof(RegistrationJourney.DirectLargeProducer), true)]
    [InlineData(nameof(RegistrationJourney.CsoLargeProducer), false)]
    public void The_compliance_scheme_must_match_the_journey(string journey, bool withComplianceScheme)
    {
        // Domain types are internal, so theory data passes names rather than enum values.
        var result = RegistrationSubmission.Start(
            TestUsers.Producer, TestUsers.ProducerOrganisationId, withComplianceScheme ? Guid.NewGuid() : null,
            2026, Enum.Parse<RegistrationJourney>(journey), Nation.England, "PEPR00012345", s_now);

        Assert.Equal("registration.compliance_scheme_mismatch", result.Error?.Code);
    }

    [Fact]
    public void A_regulator_cannot_decide_before_the_application_is_submitted()
    {
        var submission = RegistrationTestData.Started();

        var result = submission.RecordDecision(
            RegulatorDecision.Grant, TestUsers.Regulator, null, null, s_now);

        Assert.Equal(ErrorType.Conflict, result.Error?.Type);
        Assert.Equal(RegistrationStatus.InProgress, submission.Status);
    }

    [Fact]
    public void A_grant_makes_the_registration_granted_and_raises_a_decision_event()
    {
        var submission = RegistrationTestData.SubmittedForApproval();

        submission.RecordDecision(RegulatorDecision.Grant, TestUsers.Regulator, null, null, s_now).ShouldSucceed();

        Assert.Equal(RegistrationStatus.Granted, submission.Status);
        Assert.Contains(submission.DomainEvents, e => e is RegistrationDecisionRecorded { Decision: RegulatorDecision.Grant });
    }

    [Fact]
    public void Cancelling_records_the_effective_date()
    {
        var submission = RegistrationTestData.SubmittedForApproval();
        submission.RecordDecision(RegulatorDecision.Grant, TestUsers.Regulator, null, null, s_now).ShouldSucceed();
        var effective = s_now.AddDays(7);

        submission.RecordDecision(RegulatorDecision.Cancel, TestUsers.Regulator, "Ceased trading", effective, s_now.AddDays(1))
            .ShouldSucceed();

        Assert.Equal(RegistrationStatus.Cancelled, submission.Status);
        Assert.Equal(effective, submission.History[^1].EffectiveDate);
    }

    [Fact]
    public void A_queried_registration_can_be_submitted_again()
    {
        var submission = RegistrationTestData.SubmittedForApproval();
        submission.RecordDecision(RegulatorDecision.Query, TestUsers.Regulator, "Please explain", null, s_now)
            .ShouldSucceed();

        submission.SubmitApplication(TestUsers.Producer, "Explained", s_now.AddDays(1)).ShouldSucceed();

        Assert.Equal(RegistrationStatus.SubmittedForApproval, submission.Status);
    }

    [Theory]
    // Decisions taken so far, then what the regulator can do next.
    [InlineData("", "Grant,Refuse,Query")]
    [InlineData("Query", "Grant,Refuse,Query")]
    [InlineData("Grant", "Cancel")]
    [InlineData("Refuse", "")]
    [InlineData("Grant,Cancel", "")]
    public void The_available_regulator_decisions_follow_the_status(string taken, string expected)
    {
        var submission = RegistrationTestData.SubmittedForApproval();
        foreach (var decision in Decisions(taken))
            submission.RecordDecision(decision, TestUsers.Regulator, "comment", null, s_now).ShouldSucceed();

        Assert.Equal(Decisions(expected), submission.AvailableRegulatorDecisions());
    }

    private static RegulatorDecision[] Decisions(string names) =>
        names.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<RegulatorDecision>).ToArray();

    [Fact]
    public void A_registration_that_is_not_started_cannot_be_submitted_twice()
    {
        var submission = RegistrationTestData.SubmittedForApproval();

        var result = submission.SubmitApplication(TestUsers.Producer, null, s_now);

        Assert.Equal("registration.submission_not_allowed", result.Error?.Code);
    }
}
