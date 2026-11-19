using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;
using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackagingApi.Test.Modules.Registration;

/// <summary>
/// Endpoint tests run the real host: authentication, built-in validation, the user filters, handlers,
/// the domain and the ProblemDetails mapping. Only the repository is swapped for an in-memory one,
/// so no database is needed.
/// </summary>
public class RegistrationEndpointsTest
{
    private readonly InMemoryRegistrationSubmissionRepository _repository = new();

    [Fact]
    public async Task A_request_with_a_bad_shape_is_a_400_validation_problem()
    {
        using var client = Client(TestUsers.Producer);

        var response = await client.PostAsJsonAsync("/registrations", new { registrationYear = 1999 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await Json(response);
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("OrganisationId", out _), errors.ToString());
        Assert.True(errors.TryGetProperty("RegistrationYear", out _), errors.ToString());
        Assert.Equal(0, _repository.Saves);
    }

    [Fact]
    public async Task A_command_without_a_user_is_a_400()
    {
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
        var submission = RegistrationTestData.SubmittedForApproval();
        _repository.Seed(submission);
        using var client = Client(TestUsers.Producer);

        var response = await client.PostAsJsonAsync(
            $"/regulator/registrations/{submission.Id}/decisions", new { decision = "Grant" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var problem = await Json(response);
        Assert.Equal("user.regulators_only", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, _repository.Saves);
    }

    [Fact]
    public async Task A_regulator_cannot_start_a_registration()
    {
        using var client = Client(TestUsers.Regulator);

        var response = await client.PostAsJsonAsync("/registrations", ValidStartRequest(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var problem = await Json(response);
        Assert.Equal("user.producers_only", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_invalid_transition_is_a_409_with_a_stable_code()
    {
        var submission = RegistrationTestData.Started();
        _repository.Seed(submission);
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
        var submission = RegistrationTestData.SubmittedForApproval();
        _repository.Seed(submission);
        using var client = Client(TestUsers.Regulator);

        var response = await client.PostAsJsonAsync(
            $"/regulator/registrations/{submission.Id}/decisions", new { decision = "Grant" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await Json(response);
        Assert.Equal("Granted", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, _repository.Saves);
    }

    [Fact]
    public async Task A_producer_starts_a_registration_and_gets_a_location()
    {
        using var client = Client(TestUsers.Producer);

        var response = await client.PostAsJsonAsync("/registrations", ValidStartRequest(), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("/registrations/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Commands_need_the_write_scope()
    {
        using var client = Client(TestUsers.Regulator, withWriteScope: false);

        var response = await client.PostAsJsonAsync(
            $"/regulator/registrations/{Guid.NewGuid()}/decisions", new { decision = "Grant" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Calls_without_service_credentials_are_unauthorised()
    {
        await using var factory = new AclTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/registrations/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
        var settings = withWriteScope
            ? new Dictionary<string, string?> { [$"Acl:Clients:{AclTestFactory.ClientId}:Scopes:1"] = "write" }
            : null;

        var factory = new AclTestFactory(settings).WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRegistrationSubmissionRepository>();
            services.AddSingleton<IRegistrationSubmissionRepository>(_repository);
        }));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = AclTestFactory.Basic(AclTestFactory.ClientId, AclTestFactory.Secret);

        foreach (var (name, value) in userHeaders ?? [])
            client.DefaultRequestHeaders.Add(name, value);

        return client;
    }

    private static async Task<JsonDocument> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
}
