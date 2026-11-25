using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Serilog.Events;
using WasteSubmitRegPackagingApi.Obligations.Services;

namespace WasteSubmitRegPackagingApi.Test.Http.Obligations;

/// <summary>
/// The PRR asks for a timeout on every call out of the service, proven by tests: when epr-common-data-api
/// stops answering, callers get a prompt 504 rather than waiting on a hung request. The resilience pipeline
/// (total timeout, retry, attempt timeout) is the one Manage Obligations uses for its downstream clients.
/// </summary>
[Collection(CapturedLogsCollection.Name)]
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
    public async Task The_total_timeout_stops_the_call_even_when_one_attempt_is_allowed_longer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var commonDataApi = new FakeCommonDataApi(hangingCalls: int.MaxValue);
        // Only the total timeout can end the call before the first attempt's 3 seconds are up.
        await using var factory = AppWith(commonDataApi, totalTimeout: "00:00:01", attemptTimeout: "00:00:03");
        using var client = CreateAuthenticatedClient(factory);

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync(Endpoint, cancellationToken);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2.5));
        Assert.Equal(1, commonDataApi.Calls);
    }

    [Fact]
    public async Task Returns_504_when_common_data_api_sends_headers_but_never_finishes_the_body()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var commonDataApi = new FakeCommonDataApi(hangingCalls: 0, stallBody: true);
        await using var factory = AppWith(commonDataApi, totalTimeout: "00:00:01", attemptTimeout: "00:00:00.4");
        using var client = CreateAuthenticatedClient(factory);

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync(Endpoint, cancellationToken);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task The_request_log_records_the_504_the_caller_receives()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var traceId = Guid.NewGuid().ToString();
        var commonDataApi = new FakeCommonDataApi(hangingCalls: int.MaxValue);
        await using var factory = AppWith(
            commonDataApi, totalTimeout: "00:00:01", attemptTimeout: "00:00:00.4", CapturedLogs.Settings);
        using var client = CreateAuthenticatedClient(factory);
        client.DefaultRequestHeaders.Add("x-cdp-request-id", traceId);

        var response = await client.GetAsync(Endpoint, cancellationToken);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        var requestLog = await CapturedLogs.RequestCompleted(traceId, cancellationToken);
        Assert.NotNull(requestLog);
        Assert.Equal(504, Assert.IsType<ScalarValue>(requestLog.Properties["StatusCode"]).Value);
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

    private static WebApplicationFactory<Program> AppWith(
        FakeCommonDataApi commonDataApi,
        string totalTimeout,
        string attemptTimeout,
        IReadOnlyDictionary<string, string?>? extraSettings = null)
    {
        var settings = new Dictionary<string, string?>(extraSettings ?? new Dictionary<string, string?>())
        {
            ["CommonDataApi:BaseUrl"] = "https://common-data-api.test",
            ["CommonDataApi:TotalRequestTimeout:Timeout"] = totalTimeout,
            ["CommonDataApi:AttemptTimeout:Timeout"] = attemptTimeout,
            ["CommonDataApi:Retry:Delay"] = "00:00:00"
        };

        return new AclTestFactory(settings).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services
                .AddHttpClient<IApprovedSubmissionsProvider, CommonDataApiApprovedSubmissionsProvider>()
                .ConfigurePrimaryHttpMessageHandler(() => commonDataApi)));
    }

    /// <summary>
    /// Stands in for epr-common-data-api: the first <c>hangingCalls</c> requests never get an answer, like a service
    /// that has stopped responding, and later ones get an empty result. With <c>stallBody</c>, the answer's headers
    /// arrive but its body never finishes.
    /// </summary>
    private sealed class FakeCommonDataApi(int hangingCalls, bool stallBody = false) : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) <= hangingCalls)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            HttpContent content = stallBody ? new StreamContent(new NeverEndingStream()) : new StringContent("[]");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    /// <summary>A response body that never sends a byte and never ends, until the reader gives up.</summary>
    private sealed class NeverEndingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
