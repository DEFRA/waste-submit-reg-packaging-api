using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.Packaging.Domain;
using WasteSubmitRegPackaging.Packaging.Infrastructure;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Persistence;
using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackagingApi.Test.ServiceTests.Packaging;

/// <summary>
/// Saves and reloads a packaging submission through EF on a Postgres built from the real Liquibase changelog, which
/// proves the C# mapping and the changesets agree, and checks the rules the database holds. Needs Docker; skips
/// without it.
/// </summary>
public sealed class PackagingDatabaseTest(ChangelogDatabase database) : IClassFixture<ChangelogDatabase>
{
    private static readonly DateTimeOffset s_now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_resubmission_round_trips_with_its_files_cycles_and_outbox()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        var submission = Accepted(NewProducer());
        var file = submission.StartFileUpload(TestUsers.Producer, "token-2", s_now.AddMinutes(10)).Value;
        submission.RecordFileStored(file, new StoredFile("second.csv", "bucket", "upload/second"), s_now.AddMinutes(11));
        Assert.True(submission.RecordFileValidated(file, new PackagingFileCheck(0, 4), s_now.AddMinutes(12)).IsSuccess);
        Assert.True(submission.RecordResubmissionFeePayment("PAY-123", s_now.AddMinutes(13)).IsSuccess);

        await using (var context = CreateContext())
        {
            context.PackagingSubmissions.Add(submission);
            await context.SaveChangesAsync(Ct);
        }

        await using var read = CreateContext();
        var loaded = await read.PackagingSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id, Ct);

        Assert.Equal(PackagingSubmissionStatus.InProgress, loaded.Status);
        Assert.Equal(2, loaded.Cycles.Count);
        Assert.Equal(2, loaded.Files.Count);
        Assert.Equal(PackagingRegulatorDecision.Accepted, loaded.Cycles.Single(c => c.CycleNumber == 1).Decision);
        Assert.True(loaded.CurrentCycle.IsResubmission);
        Assert.Equal("PAY-123", loaded.CurrentCycle.PaymentReference);
        Assert.Equal(s_now.AddMinutes(12), loaded.LatestFile!.ValidatedAt);
        Assert.Equal(("second.csv", "upload/second", 4), (loaded.LatestFile!.FileName, loaded.LatestFile.S3Key, loaded.LatestFile.WarningCount));
        Assert.Equal(PackagingNextStep.SubmitToRegulator, loaded.NextStep());

        // Started, submitted, decided: each domain event is in the outbox, saved with the submission.
        var outbox = await read.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pkg_outbox_message WHERE payload ->> 'packagingSubmissionId' = {submission.Id.Value.ToString()}")
            .SingleAsync(Ct);
        Assert.Equal(3, outbox);
    }

    [Fact]
    public async Task A_submitter_has_one_submission_for_a_period_even_with_no_compliance_scheme()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        var producer = NewProducer();

        await using var first = CreateContext();
        var firstRepository = new PackagingSubmissionRepository(first);
        firstRepository.Add(Started(producer));
        Assert.True((await firstRepository.SaveChangesAsync(Ct)).IsSuccess);

        await using var second = CreateContext();
        var secondRepository = new PackagingSubmissionRepository(second);
        secondRepository.Add(Started(producer));
        var saved = await secondRepository.SaveChangesAsync(Ct);

        Assert.Equal("packaging.concurrent_change", saved.Error?.Code);
    }

    private static PackagingSubmission Started(ProducerUser producer)
    {
        var submission = PackagingSubmission.Start(
            producer, producer.OrganisationId, complianceSchemeId: null, "July to December 2025", 2025, 2026, s_now);
        submission.StartFileUpload(producer, "token-1", s_now);
        return submission;
    }

    private static PackagingSubmission Accepted(ProducerUser producer)
    {
        var submission = Started(producer);
        var file = submission.LatestFile!;
        submission.RecordFileStored(file, new StoredFile("first.csv", "bucket", "upload/first"), s_now.AddMinutes(1));
        Assert.True(submission.RecordFileValidated(file, PackagingFileCheck.Passed, s_now.AddMinutes(1)).IsSuccess);
        Assert.True(submission.SubmitToRegulator(producer, file.Id, "A Producer", s_now.AddMinutes(2)).IsSuccess);
        Assert.True(submission.RecordRegulatorDecision(PackagingRegulatorDecision.Accepted, TestUsers.Regulator, null, s_now.AddMinutes(3)).IsSuccess);
        return submission;
    }

    private static ProducerUser NewProducer() => new(Guid.NewGuid(), Guid.NewGuid());

    private PackagingDbContext CreateContext() =>
        new(database.Options<PackagingDbContext>(options => options.AddInterceptors(new DomainEventsToOutboxInterceptor())));
}
