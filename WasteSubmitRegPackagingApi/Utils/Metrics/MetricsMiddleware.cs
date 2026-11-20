namespace WasteSubmitRegPackagingApi.Utils.Metrics;

/// <summary>Records every request's route, method, status and duration, and any unhandled exception.</summary>
public class MetricsMiddleware(IRequestMetrics requestMetrics) : IMiddleware
{
    // Unmatched URLs share one value, so scanners can't create a metric series per path they try.
    private const string UnknownPath = "unknown";

    // The convention ASP.NET Core's own exception handlers use for a caller that went away.
    private const int ClientClosedRequest = 499;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var startingTimestamp = TimeProvider.System.GetTimestamp();
        // Read before the request runs: an exception handler clears the endpoint before writing its response.
        var path = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? UnknownPath;
        var statusCode = (int?)null;

        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The caller gave up and received nothing. That isn't a fault in this service.
            statusCode = ClientClosedRequest;
            throw;
        }
        catch (Exception ex)
        {
            // The status is still the default 200 while the exception unwinds. Unless a response was already
            // sent, the caller gets a 500, so that is what is recorded. Manage Obligations records the 200.
            statusCode = context.Response.HasStarted ? context.Response.StatusCode : StatusCodes.Status500InternalServerError;
            requestMetrics.RequestFaulted(path, context.Request.Method, statusCode.Value, ex);
            throw;
        }
        finally
        {
            requestMetrics.RequestCompleted(
                path,
                context.Request.Method,
                statusCode ?? context.Response.StatusCode,
                TimeProvider.System.GetElapsedTime(startingTimestamp).TotalMilliseconds);
        }
    }
}
