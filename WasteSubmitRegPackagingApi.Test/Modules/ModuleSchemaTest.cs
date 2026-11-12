using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.Registration.Infrastructure;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Persistence;
using WasteSubmitRegPackagingApi.Test.Modules.Registration;

namespace WasteSubmitRegPackagingApi.Test.Modules;

/// <summary>
/// Applies the real Liquibase changelog to a throwaway Postgres, then saves and reloads an aggregate
/// through EF. This is what proves the C# mapping and the changelog agree. It needs Docker, and skips
/// without it, as in the Dockerfile build.
/// </summary>
public sealed class ModuleSchemaTest : IAsyncLifetime
{
    private const string LiquibaseImage = "liquibase/liquibase:5.0.1";

    private INetwork? _network;
    private PostgreSqlContainer? _postgres;
    private string? _skipReason;

    public async ValueTask InitializeAsync()
    {
        try
        {
            _network = new NetworkBuilder().Build();
            await _network.CreateAsync();

            _postgres = new PostgreSqlBuilder("postgres:16-alpine")
                .WithNetwork(_network)
                .WithNetworkAliases("postgres")
                .Build();
            await _postgres.StartAsync();

            await ApplyChangelogAsync();
        }
        catch (DockerUnavailableException)
        {
            _skipReason = "Docker is not available, so there is no Postgres to apply the changelog to.";
        }
    }

    [Fact]
    public async Task A_registration_round_trips_through_the_changelog_schema_with_its_history_and_outbox()
    {
        Assert.SkipWhen(_skipReason is not null, _skipReason ?? "");
        var ct = TestContext.Current.CancellationToken;

        var submission = RegistrationTestData.SubmittedForApproval();
        submission.RecordDecision(
                RegulatorDecision.Grant, TestActors.Regulator, null, null, new FixedReferenceNumberGenerator(), RegistrationTestData.Now)
            .ShouldSucceed();

        await using (var write = CreateContext())
        {
            write.RegistrationSubmissions.Add(submission);
            await write.SaveChangesAsync(ct);
        }

        await using var read = CreateContext();
        var loaded = await read.RegistrationSubmissions.SingleAsync(s => s.Id == submission.Id, ct);

        Assert.Equal(RegistrationStatus.Granted, loaded.Status);
        Assert.Equal("R26EP00000001", loaded.RegistrationReferenceNumber);
        Assert.Equal(3, loaded.History.Count);

        // Started, ApplicationSubmitted and DecisionRecorded, all in the same transaction as the change.
        var outbox = await read.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM reg_outbox_message")
            .SingleAsync(ct);
        Assert.Equal(3, outbox);
    }

    [Fact]
    public async Task Two_concurrent_decisions_on_one_registration_fail_the_second_save()
    {
        Assert.SkipWhen(_skipReason is not null, _skipReason ?? "");
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

        firstCopy.RecordDecision(RegulatorDecision.Grant, TestActors.Regulator, null, null, new FixedReferenceNumberGenerator(), RegistrationTestData.Now)
            .ShouldSucceed();
        secondCopy.RecordDecision(RegulatorDecision.Refuse, TestActors.Regulator, "No", null, new FixedReferenceNumberGenerator(), RegistrationTestData.Now)
            .ShouldSucceed();

        await first.SaveChangesAsync(ct);
        var repository = new RegistrationSubmissionRepository(second);

        var result = await repository.SaveChangesAsync(ct);

        Assert.Equal("registration.concurrent_change", result.Error?.Code);
    }

    public async ValueTask DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
        if (_network is not null)
            await _network.DisposeAsync();
    }

    private RegistrationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<RegistrationDbContext>()
            .UseNpgsql(_postgres!.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new DomainEventsToOutboxInterceptor())
            .Options);

    private async Task ApplyChangelogAsync()
    {
        var connection = new NpgsqlConnectionStringBuilder(_postgres!.GetConnectionString());
        var changelog = Path.Combine(AppContext.BaseDirectory, "changelog");

        await using var liquibase = new ContainerBuilder(LiquibaseImage)
            .WithNetwork(_network)
            .WithResourceMapping(new DirectoryInfo(changelog), "/liquibase/changelog/")
            .WithEntrypoint("/bin/sh", "-c")
            .WithCommand(
                "liquibase lpm add postgresql --global && " +
                "liquibase --search-path=/liquibase/changelog --changelog-file=db.changelog.xml " +
                $"--url=jdbc:postgresql://postgres:5432/{connection.Database} " +
                $"--username={connection.Username} --password={connection.Password} update")
            .Build();

        await liquibase.StartAsync();
        var exitCode = await liquibase.GetExitCodeAsync();
        if (exitCode != 0)
        {
            var (stdout, stderr) = await liquibase.GetLogsAsync();
            throw new InvalidOperationException($"Liquibase update failed ({exitCode}).\n{stdout}\n{stderr}");
        }
    }
}
