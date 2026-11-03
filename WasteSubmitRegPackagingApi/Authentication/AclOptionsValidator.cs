using Microsoft.Extensions.Options;

namespace WasteSubmitRegPackagingApi.Authentication;

/// <summary>
/// Validates each client in the access control list. Data annotations only check the top-level
/// <see cref="AclOptions"/> and never look inside the <c>Clients</c> dictionary, so without this a
/// half-configured client (no Type, or an ApiKey client with no Secret) would start fine and then
/// fail every call with a 401.
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
                failures.Add($"Acl:Clients:{clientId}:Type '{client.Type}' is not a known client type.");

            if (client.Type == AclOptions.ClientType.ApiKey && string.IsNullOrWhiteSpace(client.Secret))
                failures.Add($"Acl:Clients:{clientId}:Secret is required for an ApiKey client.");

            if (client.Scopes is null || client.Scopes.Length == 0)
                failures.Add($"Acl:Clients:{clientId}:Scopes must contain at least one scope.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
