using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Polly.Timeout;
using WasteSubmitRegPackagingApi.Obligations.Services;

namespace WasteSubmitRegPackagingApi.Test.Http.Platform;

/// <summary>
/// The custom Grafana dashboard charts requests, latency and faults from these metrics. Names and tags match
/// Manage Obligations, so its dashboard panels can be reused.
/// </summary>
public class RequestMetricsTest
{
    private const string MeterName = "Defra.WasteSubmitRegPackagingApi";
    private const string Endpoint = "/packaging/2024/producer-material-totals-by-submitter";
    private const string EndpointPattern = "/packaging/{packagingYear:int}/producer-material-totals-by-submitter";
    private static readonly TimeSpan s_wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task A_successful_request_is_counted_with_its_route_method_and_status()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var received = Collect<long>(factory, "RequestReceived");
        using var duration = Collect<double>(factory, "RequestDuration");
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync(Endpoint, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await received.WaitForMeasurementsAsync(1, s_wait);
        await duration.WaitForMeasurementsAsync(1, s_wait);

        var request = Assert.Single(received.GetMeasurementSnapshot());
        Assert.Equal(1, request.Value);
        Assert.Equal(EndpointPattern, request.Tags["RequestPath"]);
        Assert.Equal("GET", request.Tags["HttpMethod"]);
        Assert.Equal(200, request.Tags["StatusCode"]);
        Assert.True(Assert.Single(duration.GetMeasurementSnapshot()).Value > 0);
    }

    [Fact]
    public async Task An_unknown_route_is_recorded_as_unknown()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var received = Collect<long>(factory, "RequestReceived");
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync("/does-not-exist", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await received.WaitForMeasurementsAsync(1, s_wait);
        var request = Assert.Single(received.GetMeasurementSnapshot());
        // Never the raw URL: scanners would otherwise create a metric series per path they try.
        Assert.Equal("unknown", request.Tags["RequestPath"]);
        Assert.Equal(404, request.Tags["StatusCode"]);
    }

    [Fact]
    public async Task An_unauthenticated_request_is_counted_as_401_and_not_a_fault()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var received = Collect<long>(factory, "RequestReceived");
        using var faulted = Collect<long>(factory, "RequestFaulted");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Endpoint, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await received.WaitForMeasurementsAsync(1, s_wait);
        Assert.Equal(401, Assert.Single(received.GetMeasurementSnapshot()).Tags["StatusCode"]);
        Assert.Empty(faulted.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task Health_checks_are_recorded_under_their_own_path()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var received = Collect<long>(factory, "RequestReceived");
        using var client = factory.CreateClient();

        await client.GetAsync("/health", cancellationToken);

        // CDP polls /health constantly; its own path lets the dashboard leave it out.
        await received.WaitForMeasurementsAsync(1, s_wait);
        Assert.Equal("/health", Assert.Single(received.GetMeasurementSnapshot()).Tags["RequestPath"]);
    }

    [Fact]
    public async Task An_unhandled_exception_is_a_fault_with_the_status_the_caller_got()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = AppWith(new UnreachableCommonDataApi());
        using var received = Collect<long>(factory, "RequestReceived");
        using var faulted = Collect<long>(factory, "RequestFaulted");
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync(Endpoint, cancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await faulted.WaitForMeasurementsAsync(1, s_wait);
        await received.WaitForMeasurementsAsync(1, s_wait);

        var fault = Assert.Single(faulted.GetMeasurementSnapshot());
        Assert.Equal(500, fault.Tags["StatusCode"]);
        Assert.Equal(nameof(HttpRequestException), fault.Tags["ExceptionType"]);
        Assert.Equal(500, Assert.Single(received.GetMeasurementSnapshot()).Tags["StatusCode"]);
    }

    [Fact]
    public async Task A_timeout_is_a_fault_recorded_as_504()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // Three 0.2 second attempts all time out well inside the 1 second total. The HttpClient backstop is also
        // 1 second, so a total that ran out instead would race it and throw a different exception.
        await using var factory = AppWith(new HangingCommonDataApi(), new Dictionary<string, string?>
        {
            ["CommonDataApi:TotalRequestTimeout:Timeout"] = "00:00:01",
            ["CommonDataApi:AttemptTimeout:Timeout"] = "00:00:00.2",
            ["CommonDataApi:Retry:Delay"] = "00:00:00"
        });
        using var received = Collect<long>(factory, "RequestReceived");
        using var faulted = Collect<long>(factory, "RequestFaulted");
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync(Endpoint, cancellationToken);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        await faulted.WaitForMeasurementsAsync(1, s_wait);
        await received.WaitForMeasurementsAsync(1, s_wait);

        var fault = Assert.Single(faulted.GetMeasurementSnapshot());
        Assert.Equal(504, fault.Tags["StatusCode"]);
        Assert.Equal(nameof(TimeoutRejectedException), fault.Tags["ExceptionType"]);
        Assert.Equal(EndpointPattern, fault.Tags["RequestPath"]);
        Assert.Equal(504, Assert.Single(received.GetMeasurementSnapshot()).Tags["StatusCode"]);
    }

    private static WebApplicationFactory<Program> AppWith(
        HttpMessageHandler commonDataApi,
        IReadOnlyDictionary<string, string?>? extraSettings = null)
    {
        var settings = new Dictionary<string, string?>(extraSettings ?? new Dictionary<string, string?>())
        {
            ["CommonDataApi:BaseUrl"] = "https://common-data-api.test"
        };

        return new AclTestFactory(settings).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services
                .AddHttpClient<IApprovedSubmissionsProvider, CommonDataApiApprovedSubmissionsProvider>()
                .ConfigurePrimaryHttpMessageHandler(() => commonDataApi)));
    }

    [Fact]
    public async Task A_caller_that_gives_up_is_recorded_as_499_and_not_a_fault()
    {
        // Manage Obligations abandons calls that take too long. Nothing failed here, and the caller got no response.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = AppWith(new HangingCommonDataApi());
        using var received = Collect<long>(factory, "RequestReceived");
        using var faulted = Collect<long>(factory, "RequestFaulted");
        using var client = CreateAuthenticatedClient(factory);
        using var callerGivesUp = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        callerGivesUp.CancelAfter(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync(Endpoint, callerGivesUp.Token));

        await received.WaitForMeasurementsAsync(1, s_wait);
        Assert.Equal(499, Assert.Single(received.GetMeasurementSnapshot()).Tags["StatusCode"]);
        Assert.Empty(faulted.GetMeasurementSnapshot());
    }

    [Fact]
    public void Startup_fails_when_metrics_are_enabled_without_a_namespace()
    {
        // On CDP the platform sets AWS_EMF_NAMESPACE. Without it, metrics would go nowhere unnoticed.
        using var factory = new AclTestFactory(new Dictionary<string, string?>
        {
            ["AWS_EMF_ENABLED"] = "true",
            ["AWS_EMF_ENVIRONMENT"] = "",
            ["AWS_EMF_NAMESPACE"] = ""
        });

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("AWS_EMF_NAMESPACE", exception.Message);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new AclTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IApprovedSubmissionsProvider, StubApprovedSubmissionsProvider>()));

    private static MetricCollector<T> Collect<T>(WebApplicationFactory<Program> factory, string instrument)
        where T : struct =>
        new(factory.Services.GetRequiredService<IMeterFactory>(), MeterName, instrument);

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = AclTestFactory.Basic(AclTestFactory.ClientId, AclTestFactory.Secret);
        return client;
    }

    /// <summary>Stands in for epr-common-data-api when it can't be reached at all.</summary>
    private sealed class UnreachableCommonDataApi : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused");
    }

    /// <summary>Stands in for epr-common-data-api when it never answers.</summary>
    private sealed class HangingCommonDataApi : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new UnreachableException();
        }
    }
}
