using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using WasteSubmitRegPackagingApi.Utils.Metrics;

namespace WasteSubmitRegPackagingApi.Test.Unit.Platform;

public class MetricsMiddlewareTest
{
    private const string Pattern = "/packaging/{packagingYear:int}/producer-material-totals-by-submitter";

    [Fact]
    public async Task The_route_is_the_one_matched_before_anything_later_clears_it()
    {
        // An exception handler clears the endpoint before it writes its response, so a route read afterwards
        // would come back as unknown for exactly the requests that failed.
        var metrics = new RecordingRequestMetrics();
        var context = new DefaultHttpContext();
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse(Pattern), 0, null, null));

        await new MetricsMiddleware(metrics).InvokeAsync(context, ctx =>
        {
            ctx.SetEndpoint(null);
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        });

        Assert.Equal(Pattern, Assert.Single(metrics.Completed).RequestPath);
    }

    [Fact]
    public async Task An_exception_after_the_response_started_is_recorded_with_the_status_already_sent()
    {
        var metrics = new RecordingRequestMetrics();
        var context = new DefaultHttpContext();
        var response = new StartedResponseFeature();
        context.Features.Set<IHttpResponseFeature>(response);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new MetricsMiddleware(metrics).InvokeAsync(context, _ =>
            {
                response.StatusCode = StatusCodes.Status202Accepted;
                response.Started = true;
                throw new InvalidOperationException("Failed part way through the body");
            }));

        Assert.Equal(202, Assert.Single(metrics.Faulted).StatusCode);
        Assert.Equal(202, Assert.Single(metrics.Completed).StatusCode);
    }

    [Theory]
    [InlineData("FOO")]
    [InlineData("PROPFIND")]
    public async Task Non_standard_methods_are_recorded_as_other(string method)
    {
        // Any token is a valid method, so recording them as sent would let a caller create a metric series per method.
        var metrics = new RecordingRequestMetrics();
        var context = new DefaultHttpContext();
        context.Request.Method = method;

        await new MetricsMiddleware(metrics).InvokeAsync(context, _ => Task.CompletedTask);

        Assert.Equal("OTHER", Assert.Single(metrics.Completed).HttpMethod);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("OPTIONS")]
    public async Task Standard_methods_are_recorded_as_sent(string method)
    {
        var metrics = new RecordingRequestMetrics();
        var context = new DefaultHttpContext();
        context.Request.Method = method;

        await new MetricsMiddleware(metrics).InvokeAsync(context, _ => Task.CompletedTask);

        Assert.Equal(method, Assert.Single(metrics.Completed).HttpMethod);
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public bool Started { get; set; }

        public override bool HasStarted => Started;
    }

    private sealed class RecordingRequestMetrics : IRequestMetrics
    {
        public List<(string RequestPath, string HttpMethod, int StatusCode)> Completed { get; } = [];
        public List<(string RequestPath, string HttpMethod, int StatusCode, Exception Exception)> Faulted { get; } = [];

        public void RequestCompleted(string requestPath, string httpMethod, int statusCode, double milliseconds) =>
            Completed.Add((requestPath, httpMethod, statusCode));

        public void RequestFaulted(string requestPath, string httpMethod, int statusCode, Exception exception) =>
            Faulted.Add((requestPath, httpMethod, statusCode, exception));
    }
}
