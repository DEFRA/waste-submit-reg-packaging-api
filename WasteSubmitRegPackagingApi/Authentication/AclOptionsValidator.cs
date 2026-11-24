using Microsoft.Extensions.Options;

namespace WasteSubmitRegPackagingApi.Authentication;

/// <summary>
/// Validates each client in the access control list. Data annotations only check the top-level
/// <see cref="AclOptions"/> and never look inside the <c>Clients</c> dictionary, so without this a
/// half-configured client (no Type, an unsupported Type, an ApiKey client with no Secret, or a
/// mistyped scope) would start fine and then fail every call with a 401 or 403. A Type the binder
/// cannot convert drops the client before it gets here; <see cref="AclClientTypeValidator"/> catches
/// that.
/// </summary>
public sealed class AclOptionsValidator : IValidateOptions<AclOptions>
{
    public ValidateOptionsResult Validate(string? name, AclOptions options)
    {
        var failures = new List<string>();

        foreach (var (clientId, client) in options.Clients)
        {
            if (client.Type is null)
                failures.Add($"Acl:Clients:{clientId}:Type is required.");
            else if (!Enum.IsDefined(client.Type.Value))
                failures.Add(UnsupportedType(clientId, client.Type.Value.ToString()));

            if (client.Type == AclOptions.ClientType.ApiKey && string.IsNullOrWhiteSpace(client.Secret))
                failures.Add($"Acl:Clients:{clientId}:Secret is required for an ApiKey client.");

            if (client.Scopes is null || client.Scopes.Length == 0)
                failures.Add($"Acl:Clients:{clientId}:Scopes must contain at least one scope.");
            else
                failures.AddRange(client.Scopes
                    .Where(scope => !Scopes.All.Contains(scope, StringComparer.Ordinal))
                    .Select(scope => $"Acl:Clients:{clientId}:Scopes '{scope}' is not a known scope ({string.Join(", ", Scopes.All)})."));
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    internal static string UnsupportedType(string clientId, string? type) =>
        $"Acl:Clients:{clientId}:Type '{type}' is not supported. This service only accepts ApiKey clients.";
}
