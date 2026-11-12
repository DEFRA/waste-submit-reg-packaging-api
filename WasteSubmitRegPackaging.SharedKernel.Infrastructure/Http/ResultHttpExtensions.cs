using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;

/// <summary>
/// The single place a failed <see cref="Result"/> becomes an HTTP response. Every error is an RFC 9457
/// ProblemDetails with a stable <c>code</c> extension, so the frontends can tell errors apart without
/// parsing messages.
/// </summary>
public static class ResultHttpExtensions
{
    public static ProblemHttpResult ToProblem(this Error error) =>
        TypedResults.Problem(
            statusCode: StatusCode(error.Type),
            title: Title(error.Type),
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });

    private static int StatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string Title(ErrorType type) => type switch
    {
        ErrorType.Validation => "The request breaks a business rule",
        ErrorType.NotFound => "Not found",
        ErrorType.Conflict => "Not allowed in the current state",
        ErrorType.Forbidden => "Not allowed for this user",
        _ => "Unexpected error"
    };
}
