namespace WasteSubmitRegPackaging.SharedKernel.Results;

/// <summary>
/// Decides which HTTP status an error becomes. It is the only HTTP knowledge the application layer
/// has, and it is expressed in domain terms.
/// </summary>
public enum ErrorType
{
    /// <summary>The request breaks a business rule the caller can fix (400).</summary>
    Validation,

    /// <summary>The thing asked for does not exist (404).</summary>
    NotFound,

    /// <summary>The request is valid but not allowed in the current state, such as an invalid transition (409).</summary>
    Conflict,

    /// <summary>The caller is not allowed to do this (403).</summary>
    Forbidden
}

/// <param name="Code">Stable and machine-readable, for example <c>registration.decision_not_allowed</c>. UIs can switch on it.</param>
/// <param name="Message">For developers and logs. Not shown to end users as it is.</param>
/// <param name="Type">Which kind of failure this is, and so which HTTP status it becomes.</param>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}
