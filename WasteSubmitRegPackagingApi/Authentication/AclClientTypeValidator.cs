using Microsoft.Extensions.Options;

namespace WasteSubmitRegPackagingApi.Authentication;

/// <summary>
/// Stops the app starting when a configured client has a Type that is not a
/// <see cref="AclOptions.ClientType"/> (for example <c>OAuth</c>, or a typo).
///
/// The configuration binder does not fail on such a value: it silently leaves the whole client out
/// of <see cref="AclOptions.Clients"/>, so <see cref="AclOptionsValidator"/> never sees it and the
/// client would get a 401 on every call. This compares the raw <c>Acl:Clients</c> section with what
/// was bound.
/// </summary>
public sealed class AclClientTypeValidator(IConfiguration configuration) : IValidateOptions<AclOptions>
{
    public ValidateOptionsResult Validate(string? name, AclOptions options)
    {
        var failures = configuration.GetSection("Acl:Clients").GetChildren()
            .Where(client => !options.Clients.ContainsKey(client.Key))
            .Select(client => AclOptionsValidator.UnsupportedType(client.Key, client["Type"]))
            .ToList();

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
