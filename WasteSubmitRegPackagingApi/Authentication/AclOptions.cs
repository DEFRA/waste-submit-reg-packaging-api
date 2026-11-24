namespace WasteSubmitRegPackagingApi.Authentication;

/// <summary>
/// The access control list of services allowed to call this API.
///
/// Copied from the Manage Obligations service (DEFRA/waste-obligations). Clients are keyed by client
/// id under <c>Acl:Clients</c>. Type and scopes live in CDP app config; the secret
/// lives in CDP secrets as <c>Acl__Clients__{clientId}__Secret</c>.
/// </summary>
public class AclOptions
{
    /// <summary>
    /// Case-insensitive, like .NET configuration keys, so a client id typed with different casing in
    /// app config and CDP secrets still resolves to one client.
    /// </summary>
    public Dictionary<string, Client> Clients { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public class Client
    {
        /// <summary>
        /// Nullable so a client whose Type is missing from config fails validation at startup,
        /// rather than silently defaulting to the enum's first value. Any other value (for example
        /// <c>OAuth</c>) also stops the app starting (see <see cref="AclClientTypeValidator"/>).
        /// Every client is validated by <see cref="AclOptionsValidator"/>.
        /// </summary>
        public required ClientType? Type { get; init; }

        public string? Secret { get; init; }

        public required string[] Scopes { get; init; } = [];
    }

    public enum ClientType
    {
        ApiKey,
    }
}
