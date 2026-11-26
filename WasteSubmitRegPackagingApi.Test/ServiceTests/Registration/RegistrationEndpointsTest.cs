using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.Registration.Infrastructure;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Persistence;
using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackagingApi.Test.ServiceTests.Registration;

/// <summary>
/// Endpoint tests run the real host: authentication, built-in validation, the user filters, handlers,
/// the domain, the repository and the ProblemDetails mapping, on Postgres built from the changelog. Nothing is
/// substituted. A test that needs a registration saves one first. Needs Docker; skips without it.
/// </summary>
public sealed class RegistrationEndpointsTest(ChangelogDatabase database) : IClassFixture<ChangelogDatabase>
{
    [Fact]
    public async Task A_request_with_a_bad_shape_is_a_400_validation_problem()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        using var client = Client(TestUsers.Producer);

        var response = await client.PostAsJsonAsync("/registrations", new { registrationYear = 1999 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await Json(response);
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("OrganisationId", out _), errors.ToString());
        Assert.True(errors.TryGetProperty("RegistrationYear", out _), errors.ToString());
    }

    [Fact]
    public async Task An_enum_sent_as_a_number_that_is_not_one_of_its_values_is_a_400()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        using var client = Client(TestUsers.Producer);

        var response = await client.PostAsJsonAsync("/registrations", new
        {
            organisationId = TestUsers.ProducerOrganisationId,
            registrationYear = 2026,
            journey = 99,
            nation = 9,
            applicationReferenceNumber = "PEPR00099999"
        }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await Json(response);
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Journey", out _), errors.ToString());
        Assert.True(errors.TryGetProperty("Nation", out _), errors.ToString());
    }

    [Fact]
    public async Task Reading_a_registration_needs_a_user_of_the_right_kind()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        var id = Guid.NewGuid();
        using var noUser = ClientWithHeaders();
        using var producer = Client(TestUsers.Producer);

        var withoutUser = await noUser.GetAsync($"/registrations/{id}", Ct);
        var producerOnRegulatorView = await producer.GetAsync($"/regulator/registrations/{id}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, withoutUser.StatusCode);
        using var missing = await Json(withoutUser);
        Assert.Equal("user.missing", missing.RootElement.GetProperty("code").GetString());

        Assert.Equal(HttpStatusCode.Forbidden, producerOnRegulatorView.StatusCode);
        using var wrongKind = await Json(producerOnRegulatorView);
        Assert.Equal("user.regulators_only", wrongKind.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("1")]
    [InlineData("Producer,Regulator")]
    public async Task A_user_type_that_is_not_one_of_the_two_names_is_a_400(string userType)
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        using var client = ClientWithHeaders(new Dictionary<string, string>
        {
            [UserHeaders.UserId] = TestUsers.Regulator.UserId.ToString(),
            [UserHeaders.Type] = userType
        });

        var response = await client.PostAsJsonAsync(
            $"/regulator/registrations/{Guid.NewGuid()}/decisions", new { decision = "Grant" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await Json(response);
        Assert.Equal("user.missing", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_command_without_a_user_is_a_400()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        using var client = ClientWithHeaders();

        var response = await client.PostAsJsonAsync(
            $"/regulator/registrations/{Guid.NewGuid()}/decisions", new { decision = "Grant" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await Json(response);
        Assert.Equal("user.missing", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_producer_without_an_organisation_is_a_400()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        using var client = ClientWithHeaders(new Dictionary<string, string>
        {
            [UserHeaders.UserId] = TestUsers.Producer.UserId.ToString(),
            [UserHeaders.Type] = "Producer"
        });

        var response = await client.PostAsJsonAsync(
            $"/registrations/{Guid.NewGuid()}/application-submission", new { }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await Json(response);
        Assert.Equal("user.missing", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_producer_cannot_make_a_regulator_decision()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        var submission = await SavedAsync(RegistrationTestData.SubmittedForApproval(RegistrationTestData.NewReferenceNumber()));
        using var client = Client(TestUsers.Producer);

        var response = await client.PostAsJsonAsync(
            $"/regulator/registrations/{submission.Id}/decisions", new { decision = "Grant" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var problem = await Json(response);
        Assert.Equal("user.regulators_only", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(RegistrationStatus.SubmittedForApproval, await StatusAsync(submission.Id));
    }

    [Fact]
    public async Task A_regulator_cannot_start_a_registration()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        using var client = Client(TestUsers.Regulator);

        var response = await client.PostAsJsonAsync("/registrations", ValidStartRequest(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var problem = await Json(response);
        Assert.Equal("user.producers_only", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_invalid_transition_is_a_409_with_a_stable_code()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        var submission = await SavedAsync(RegistrationTestData.Started(RegistrationTestData.NewReferenceNumber()));
        using var client = Client(TestUsers.Regulator);

        var response = await client.PostAsJsonAsync(
            $"/regulator/registrations/{submission.Id}/decisions", new { decision = "Grant" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = await Json(response);
        Assert.Equal("registration.decision_not_allowed", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_grant_returns_the_new_status()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        var submission = await SavedAsync(RegistrationTestData.SubmittedForApproval(RegistrationTestData.NewReferenceNumber()));
        using var client = Client(TestUsers.Regulator);

        var response = await client.PostAsJsonAsync(
            $"/regulator/registrations/{submission.Id}/decisions", new { decision = "Grant" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await Json(response);
        Assert.Equal("Granted", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(RegistrationStatus.Granted, await StatusAsync(submission.Id));
    }

    [Fact]
    public async Task A_producer_starts_a_registration_and_gets_a_location()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        using var client = Client(TestUsers.Producer);

        var response = await client.PostAsJsonAsync("/registrations", ValidStartRequest(), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("/registrations/", response.Headers.Location?.OriginalString);
        using var body = await Json(response);
        var id = new RegistrationSubmissionId(body.RootElement.GetProperty("registrationSubmissionId").GetGuid());
        Assert.Equal(RegistrationStatus.InProgress, await StatusAsync(id));
    }

    [Fact]
    public async Task Commands_need_the_write_scope()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        using var client = Client(TestUsers.Regulator, withWriteScope: false);

        var response = await client.PostAsJsonAsync(
            $"/regulator/registrations/{Guid.NewGuid()}/decisions", new { decision = "Grant" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object ValidStartRequest() => new
    {
        organisationId = TestUsers.ProducerOrganisationId,
        registrationYear = 2026,
        journey = "DirectLargeProducer",
        nation = "GB-ENG",
        applicationReferenceNumber = "PEPR00099999"
    };

    private HttpClient Client(ProducerUser producer) => ClientWithHeaders(new Dictionary<string, string>
    {
        [UserHeaders.UserId] = producer.UserId.ToString(),
        [UserHeaders.Type] = "Producer",
        [UserHeaders.OrganisationId] = producer.OrganisationId.ToString()
    });

    private HttpClient Client(RegulatorUser regulator, bool withWriteScope = true) => ClientWithHeaders(new Dictionary<string, string>
    {
        [UserHeaders.UserId] = regulator.UserId.ToString(),
        [UserHeaders.Type] = "Regulator"
    }, withWriteScope);

    private HttpClient ClientWithHeaders(Dictionary<string, string>? userHeaders = null, bool withWriteScope = true)
    {
        var settings = new Dictionary<string, string?>(PostgresSettings.For(database.ConnectionDetails));
        if (withWriteScope)
            settings[$"Acl:Clients:{AclTestFactory.ClientId}:Scopes:1"] = "write";

        var client = new AclTestFactory(settings).CreateClient();
        client.DefaultRequestHeaders.Authorization = AclTestFactory.Basic(AclTestFactory.ClientId, AclTestFactory.Secret);

        foreach (var (name, value) in userHeaders ?? [])
            client.DefaultRequestHeaders.Add(name, value);

        return client;
    }

    private static async Task<JsonDocument> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    private async Task<RegistrationSubmission> SavedAsync(RegistrationSubmission submission)
    {
        await using var context = CreateContext();
        context.RegistrationSubmissions.Add(submission);
        await context.SaveChangesAsync(Ct);
        return submission;
    }

    private async Task<RegistrationStatus> StatusAsync(RegistrationSubmissionId id)
    {
        await using var context = CreateContext();
        return (await context.RegistrationSubmissions.AsNoTracking().SingleAsync(s => s.Id == id, Ct)).Status;
    }

    private RegistrationDbContext CreateContext() =>
        new(database.Options<RegistrationDbContext>(options => options.AddInterceptors(new DomainEventsToOutboxInterceptor())));
}
