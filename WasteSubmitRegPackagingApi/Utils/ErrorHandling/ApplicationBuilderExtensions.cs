using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Polly.Timeout;

namespace WasteSubmitRegPackagingApi.Utils.ErrorHandling;

/// <summary>
/// Turns unhandled exceptions into problem responses, in the same shape as Manage Obligations (waste-obligations)
/// and waste-organisations. A downstream call that runs past its timeout answers 504, so callers get a prompt,
/// clear answer when a dependency stops responding. Everything else stays a 500.
/// </summary>
public static class ApplicationBuilderExtensions
{
    public static void UseErrorHandling(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(
            new ExceptionHandlerOptions
            {
                AllowStatusCode404Response = true,
                ExceptionHandler = async context =>
                {
                    var exceptionHandlerFeature =
                        context.Features.Get<IExceptionHandlerFeature>()
                        ?? throw new InvalidOperationException("Exception handler feature is unavailable.");
                    var (statusCode, title) = Describe(exceptionHandlerFeature.Error);

                    context.Response.StatusCode = statusCode;

                    await context
                        .RequestServices.GetRequiredService<IProblemDetailsService>()
                        .WriteAsync(
                            new ProblemDetailsContext
                            {
                                HttpContext = context,
                                AdditionalMetadata = exceptionHandlerFeature.Endpoint?.Metadata,
                                ProblemDetails = new ProblemDetails { Title = title, Status = statusCode }
                            });
                }
            });
    }

    public static (int StatusCode, string Title) Describe(Exception exception) =>
        exception switch
        {
            // The framework couldn't bind the request, for example an unknown enum value. It sets the status (400).
            BadHttpRequestException badRequest => (badRequest.StatusCode, "The request is not valid"),
            // The resilience pipeline's total or attempt timeout ran out on a downstream HTTP call.
            TimeoutRejectedException => (StatusCodes.Status504GatewayTimeout, "A downstream service did not respond in time"),
            // HttpClient.Timeout ran out, which is the backstop for a response body that never finishes.
            TaskCanceledException { InnerException: TimeoutException } => (StatusCodes.Status504GatewayTimeout, "A downstream service did not respond in time"),
            // Npgsql reports both its connect and command timeouts as an NpgsqlException wrapping a TimeoutException.
            NpgsqlException { InnerException: TimeoutException } => (StatusCodes.Status504GatewayTimeout, "The database did not respond in time"),
            _ => (StatusCodes.Status500InternalServerError, "An error occurred while processing your request.")
        };
}
