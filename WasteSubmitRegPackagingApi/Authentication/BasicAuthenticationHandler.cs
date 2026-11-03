using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace WasteSubmitRegPackagingApi.Authentication;

/// <summary>
/// Authenticates service-to-service calls with an API key sent as HTTP Basic credentials
/// (<c>Authorization: Basic base64(clientId:secret)</c>), checked against <see cref="AclOptions"/>.
///
/// Copied from the Manage Obligations service (DEFRA/waste-obligations). One deliberate difference:
/// a malformed header (unparseable, or not valid base64) fails authentication instead of throwing.
/// This is the default scheme, so it runs on every request, and an exception here would turn
/// <c>/health</c> into a 500 for any caller that sends a bad header.
/// </summary>
public class BasicAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AclOptions> aclOptions
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Basic";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var endpoint = Context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null)
            return NoResult();

        var authorizationHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authorizationHeader))
            return NoResult();

        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var authenticationHeaderValue))
            return Fail();

        if (authenticationHeaderValue.Scheme != SchemeName)
            return NoResult();

        if (!TryDecodeCredentials(authenticationHeaderValue.Parameter, out var clientId, out var secret))
            return Fail();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(secret))
            return Fail();

        aclOptions.Value.Clients.TryGetValue(clientId, out var client);
        if (client is not { Type: AclOptions.ClientType.ApiKey } || client.Secret != secret)
            return Fail();

        var claims = new List<Claim> { new(ClaimTypes.Name, clientId), new(Claims.ClientId, clientId) };
        claims.AddRange(client.Scopes.Select(scope => new Claim(Claims.Scope, scope)));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Success(ticket);
    }

    private static bool TryDecodeCredentials(string? parameter, out string clientId, out string secret)
    {
        clientId = string.Empty;
        secret = string.Empty;

        var buffer = new byte[((parameter?.Length ?? 0) * 3 / 4) + 3];
        if (!Convert.TryFromBase64String(parameter ?? string.Empty, buffer, out var bytesWritten))
            return false;

        var credentials = Encoding.UTF8.GetString(buffer, 0, bytesWritten).Split(':', 2);
        clientId = credentials[0];
        secret = credentials.Length > 1 ? credentials[1] : string.Empty;

        return true;
    }

    private static Task<AuthenticateResult> NoResult() => Task.FromResult(AuthenticateResult.NoResult());

    private static Task<AuthenticateResult> Fail() => Task.FromResult(AuthenticateResult.Fail("Failed authorization"));

    private static Task<AuthenticateResult> Success(AuthenticationTicket ticket) =>
        Task.FromResult(AuthenticateResult.Success(ticket));
}
