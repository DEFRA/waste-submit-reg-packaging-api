using Microsoft.AspNetCore.Diagnostics;

namespace WasteSubmitRegPackagingApi.Utils.Metrics;

/// <summary>
/// Records every request's route, method, status and duration, and any exception, whether the error handler dealt
/// with it or not. It sits outside the error handler, so it sees the status the caller gets.
/// </summary>
public class MetricsMiddleware(IRequestMetrics requestMetrics) : IMiddleware
{
    // Unmatched URLs share one value, so scanners can't create a metric series per path they try.
    private const string UnknownPath = "unknown";

    // The convention ASP.NET Core's own exception handlers use for a caller that went away.
    private const int ClientClosedRequest = 499;

    // Any token is a valid method, so others share one value for the same reason as unknown paths.
    private const string OtherMethod = "OTHER";

    private static readonly HashSet<string> s_standardMethods = new(StringComparer.Ordinal)
    {
        HttpMethods.Get, HttpMethods.Head, HttpMethods.Post, HttpMethods.Put,
        HttpMethods.Patch, HttpMethods.Delete, HttpMethods.Options
    };

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var startingTimestamp = TimeProvider.System.GetTimestamp();
        // Read before the request runs: an exception handler clears the endpoint before writing its response.
        var path = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? UnknownPath;
        var method = s_standardMethods.Contains(context.Request.Method) ? context.Request.Method : OtherMethod;
        var statusCode = (int?)null;

        try
        {
            await next(context);

            // The error handler inside this middleware turns exceptions into problem responses (500, or 504 for a
            // timeout), so they arrive here as handled. Its record of the exception still makes them faults.
            if (context.Features.Get<IExceptionHandlerFeature>() is { } handled)
                requestMetrics.RequestFaulted(path, method, context.Response.StatusCode, handled.Error);
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
            requestMetrics.RequestFaulted(path, method, statusCode.Value, ex);
            throw;
        }
        finally
        {
            requestMetrics.RequestCompleted(
                path,
                method,
                statusCode ?? context.Response.StatusCode,
                TimeProvider.System.GetElapsedTime(startingTimestamp).TotalMilliseconds);
        }
    }
}
