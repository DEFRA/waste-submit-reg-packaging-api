using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using WasteSubmitRegPackagingApi.Obligations.Services;

namespace WasteSubmitRegPackagingApi.Test.Obligations.Endpoints;

/// <summary>
/// The PRR asks for a timeout on every call out of the service, proven by tests: when epr-common-data-api
/// stops answering, callers get a prompt 504 rather than waiting on a hung request. The resilience pipeline
/// (total timeout, retry, attempt timeout) is the one Manage Obligations uses for its downstream clients.
/// </summary>
public class CommonDataApiTimeoutTest
{
    private const string Endpoint = "/packaging/2024/producer-material-totals-by-submitter";

    [Fact]
    public async Task Returns_504_when_common_data_api_does_not_answer_within_the_total_timeout()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var commonDataApi = new FakeCommonDataApi(hangingCalls: int.MaxValue);
        await using var factory = AppWith(commonDataApi, totalTimeout: "00:00:01", attemptTimeout: "00:00:00.4");
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync(Endpoint, cancellationToken);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(504, problem.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Retries_an_attempt_that_times_out()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var commonDataApi = new FakeCommonDataApi(hangingCalls: 1);
        await using var factory = AppWith(commonDataApi, totalTimeout: "00:00:05", attemptTimeout: "00:00:00.5");
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync(Endpoint, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, commonDataApi.Calls);
    }

    [Fact]
    public void Waits_60_seconds_in_total_with_two_retries_of_20_seconds_by_default()
    {
        using var factory = new AclTestFactory();

        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>()
            .Get("CommonDataApi");

        Assert.Equal(TimeSpan.FromSeconds(60), options.TotalRequestTimeout.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(20), options.AttemptTimeout.Timeout);
        Assert.Equal(2, options.Retry.MaxRetryAttempts);
        Assert.Equal(DelayBackoffType.Exponential, options.Retry.BackoffType);
        Assert.True(options.Retry.UseJitter);
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        // Fail fast instead of waiting out HttpClient's 100 second default if the app's timeout is missing.
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Authorization = AclTestFactory.Basic(AclTestFactory.ClientId, AclTestFactory.Secret);
        return client;
    }

    private static WebApplicationFactory<Program> AppWith(FakeCommonDataApi commonDataApi, string totalTimeout, string attemptTimeout) =>
        new AclTestFactory(new Dictionary<string, string?>
        {
            ["CommonDataApi:BaseUrl"] = "https://common-data-api.test",
            ["CommonDataApi:TotalRequestTimeout:Timeout"] = totalTimeout,
            ["CommonDataApi:AttemptTimeout:Timeout"] = attemptTimeout,
            ["CommonDataApi:Retry:Delay"] = "00:00:00"
        }).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services
                .AddHttpClient<IApprovedSubmissionsProvider, CommonDataApiApprovedSubmissionsProvider>()
                .ConfigurePrimaryHttpMessageHandler(() => commonDataApi)));

    /// <summary>
    /// Stands in for epr-common-data-api: the first <c>hangingCalls</c> requests never get an answer, like a service
    /// that has stopped responding, and later ones get an empty result.
    /// </summary>
    private sealed class FakeCommonDataApi(int hangingCalls) : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) <= hangingCalls)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
        }
    }
}
