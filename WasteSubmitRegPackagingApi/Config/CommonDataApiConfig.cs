namespace WasteSubmitRegPackagingApi.Config;

using System.ComponentModel.DataAnnotations;

public class CommonDataApiConfig
{
    /// <summary>
    /// epr-common-data-api's base address. Set per environment in cdp-app-config once the
    /// cross-platform networking (Squid ACL, auth) is in place.
    /// </summary>
    [Required]
    public required string BaseUrl { get; init; }
}
