using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.Registration.Application.Queries.GetRegistrationSubmission;
using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.Registration.Infrastructure;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Persistence;
using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackagingApi.Test.Database.Registration;

/// <summary>
/// Saves and reloads a registration through EF on a Postgres built from the real Liquibase changelog
/// (<see cref="ChangelogDatabase"/>). This is what proves the C# mapping and the changelog agree. It needs
/// Docker, and skips without it, as in the Dockerfile build.
/// </summary>
public sealed class RegistrationDatabaseTest : IAsyncLifetime
{
    // A fresh database per test, because these tests write registrations with fixed reference numbers.
    private readonly ChangelogDatabase database = new();

    public ValueTask InitializeAsync() => database.InitializeAsync();

    public ValueTask DisposeAsync() => database.DisposeAsync();

    [Fact]
    public async Task A_registration_round_trips_through_the_changelog_schema_with_its_history_and_outbox()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        var ct = TestContext.Current.CancellationToken;

        var submission = RegistrationTestData.SubmittedForApproval();
        submission.RecordDecision(
                RegulatorDecision.Grant, TestUsers.Regulator, null, null, RegistrationTestData.Now)
            .ShouldSucceed();

        await using (var write = CreateContext())
        {
            write.RegistrationSubmissions.Add(submission);
            await write.SaveChangesAsync(ct);
        }

        await using var read = CreateContext();
        var loaded = await read.RegistrationSubmissions.SingleAsync(s => s.Id == submission.Id, ct);

        Assert.Equal(RegistrationStatus.Granted, loaded.Status);
        Assert.Equal(3, loaded.History.Count);

        // Started, ApplicationSubmitted and DecisionRecorded, all in the same transaction as the change.
        var outbox = await read.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM reg_outbox_message WHERE payload->>'registrationSubmissionId' = {submission.Id.Value.ToString()}")
            .SingleAsync(ct);
        Assert.Equal(3, outbox);
    }

    [Fact]
    public async Task A_producer_can_read_their_own_organisations_registration_but_not_another_organisations()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        var ct = TestContext.Current.CancellationToken;

        var submission = RegistrationTestData.Started();
        await using (var seed = CreateContext())
        {
            seed.RegistrationSubmissions.Add(submission);
            await seed.SaveChangesAsync(ct);
        }

        await using var read = CreateContext();
        var handler = new GetRegistrationSubmissionHandler(read);
        var otherOrganisationsUser = new ProducerUser(Guid.NewGuid(), Guid.NewGuid());

        var own = await handler.HandleAsync(new GetRegistrationSubmissionQuery(submission.Id, TestUsers.Producer), ct);
        var other = await handler.HandleAsync(new GetRegistrationSubmissionQuery(submission.Id, otherOrganisationsUser), ct);
        var regulator = await handler.HandleAsync(new GetRegistrationSubmissionQuery(submission.Id), ct);

        Assert.True(own.IsSuccess);
        Assert.Equal("registration.not_your_organisation", other.Error?.Code);
        Assert.True(regulator.IsSuccess);
    }

    [Fact]
    public async Task Two_concurrent_decisions_on_one_registration_fail_the_second_save()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        var ct = TestContext.Current.CancellationToken;

        var submission = RegistrationTestData.SubmittedForApproval();
        await using (var seed = CreateContext())
        {
            seed.RegistrationSubmissions.Add(submission);
            await seed.SaveChangesAsync(ct);
        }

        await using var first = CreateContext();
        await using var second = CreateContext();
        var firstCopy = await first.RegistrationSubmissions.SingleAsync(s => s.Id == submission.Id, ct);
        var secondCopy = await second.RegistrationSubmissions.SingleAsync(s => s.Id == submission.Id, ct);

        firstCopy.RecordDecision(RegulatorDecision.Grant, TestUsers.Regulator, null, null, RegistrationTestData.Now)
            .ShouldSucceed();
        secondCopy.RecordDecision(RegulatorDecision.Refuse, TestUsers.Regulator, "No", null, RegistrationTestData.Now)
            .ShouldSucceed();

        await first.SaveChangesAsync(ct);
        var repository = new RegistrationSubmissionRepository(second);

        var result = await repository.SaveChangesAsync(ct);

        Assert.Equal("registration.concurrent_change", result.Error?.Code);
    }

    private RegistrationDbContext CreateContext() =>
        new(database.Options<RegistrationDbContext>(options => options.AddInterceptors(new DomainEventsToOutboxInterceptor())));
}
