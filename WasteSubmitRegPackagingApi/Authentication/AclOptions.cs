using System.ComponentModel.DataAnnotations;

namespace WasteSubmitRegPackagingApi.Authentication;

/// <summary>
/// The access control list of services allowed to call this API.
///
/// Copied from the Manage Obligations service (DEFRA/waste-obligations). Clients are keyed by client
/// id under <c>Acl:Clients</c>. Type, scopes and allowed endpoints live in CDP app config; the secret
/// lives in CDP secrets as <c>Acl__Clients__{clientId}__Secret</c>.
/// </summary>
public class AclOptions
{
    public Dictionary<string, Client> Clients { get; init; } = new();

    public class Client
    {
        /// <summary>
        /// Nullable so a client whose Type is missing from config fails validation at startup,
        /// rather than silently defaulting to <see cref="ClientType.OAuth"/> and failing every call.
        /// </summary>
        [Required]
        public required ClientType? Type { get; init; }

        public string? Secret { get; init; }

        [Required]
        public required string[] Scopes { get; init; } = [];

        public string[] AllowedEndpoints { get; set; } = [];
    }

    public enum ClientType
    {
        OAuth,
        ApiKey,
    }
}
