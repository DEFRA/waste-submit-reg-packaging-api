using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WasteSubmitRegPackaging.Registration.Domain;
using WasteSubmitRegPackaging.SharedKernel.Actors;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;

namespace WasteSubmitRegPackagingApi.Test.Modules.Registration;

/// <summary>
/// Endpoint tests run the real host: authentication, built-in validation, the actor filter, handlers,
/// the domain and the ProblemDetails mapping. Only the repository is swapped for an in-memory one,
/// so no database is needed.
/// </summary>
public class RegistrationEndpointsTest
{
    private readonly InMemoryRegistrationSubmissionRepository _repository = new();

    [Fact]
    public async Task A_request_with_a_bad_shape_is_a_400_validation_problem()
    {
        using var client = Client(TestActors.Producer);

        var response = await client.PostAsJsonAsync("/registrations", new { registrationYear = 1999 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await Json(response);
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("OrganisationId", out _), errors.ToString());
        Assert.True(errors.TryGetProperty("RegistrationYear", out _), errors.ToString());
        Assert.Equal(0, _repository.Saves);
    }

    [Fact]
    public async Task A_command_without_an_actor_is_a_400()
    {
        using var client = Client(actor: null);

        var response = await client.PostAsJsonAsync(
            $"/regulator/registrations/{Guid.NewGuid()}/decisions", new { decision = "Grant" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await Json(response);
        Assert.Equal("actor.missing", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_invalid_transition_is_a_409_with_a_stable_code()
    {
        var submission = RegistrationTestData.Started();
        _repository.Seed(submission);
        using var client = Client(TestActors.Regulator);

        var response = await client.PostAsJsonAsync(
            $"/regulator/registrations/{submission.Id}/decisions", new { decision = "Grant" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = await Json(response);
        Assert.Equal("registration.decision_not_allowed", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_grant_returns_the_new_status_and_reference_number()
    {
        var submission = RegistrationTestData.SubmittedForApproval();
        _repository.Seed(submission);
        using var client = Client(TestActors.Regulator);

        var response = await client.PostAsJsonAsync(
            $"/regulator/registrations/{submission.Id}/decisions", new { decision = "Grant" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await Json(response);
        Assert.Equal("Granted", body.RootElement.GetProperty("status").GetString());
        Assert.StartsWith("R26EP", body.RootElement.GetProperty("registrationReferenceNumber").GetString());
        Assert.Equal(1, _repository.Saves);
    }

    [Fact]
    public async Task A_producer_starts_a_registration_and_gets_a_location()
    {
        using var client = Client(TestActors.Producer);

        var response = await client.PostAsJsonAsync("/registrations", new
        {
            organisationId = TestActors.ProducerOrganisationId,
            registrationYear = 2026,
            journey = "DirectLargeProducer",
            nation = "England",
            applicationReferenceNumber = "PEPR00099999"
        }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("/registrations/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Commands_need_the_write_scope()
    {
        using var client = Client(TestActors.Regulator, withWriteScope: false);

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

    private HttpClient Client(Actor? actor, bool withWriteScope = true)
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

        if (actor is not null)
        {
            client.DefaultRequestHeaders.Add(ActorHeaders.UserId, actor.UserId.ToString());
            client.DefaultRequestHeaders.Add(ActorHeaders.Type, actor.Type.ToString());
            if (actor.OrganisationId is { } organisationId)
                client.DefaultRequestHeaders.Add(ActorHeaders.OrganisationId, organisationId.ToString());
        }

        return client;
    }

    private static async Task<JsonDocument> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
}
