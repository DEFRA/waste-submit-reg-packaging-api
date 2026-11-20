using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using WasteSubmitRegPackagingApi.Utils.Metrics;

namespace WasteSubmitRegPackagingApi.Test.Utils.Metrics;

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

    private sealed class RecordingRequestMetrics : IRequestMetrics
    {
        public List<(string RequestPath, string HttpMethod, int StatusCode)> Completed { get; } = [];

        public void RequestCompleted(string requestPath, string httpMethod, int statusCode, double milliseconds) =>
            Completed.Add((requestPath, httpMethod, statusCode));

        public void RequestFaulted(string requestPath, string httpMethod, int statusCode, Exception exception)
        {
        }
    }
}
