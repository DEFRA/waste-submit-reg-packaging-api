namespace WasteSubmitRegPackagingApi.Utils.OpenApi;

/// <summary>The OpenAPI document names, served at <c>/openapi/{name}.json</c>.</summary>
public static class OpenApiDocuments
{
    /// <summary>The published endpoints: the merged contract, which frontends generate clients from.</summary>
    public const string V1 = "v1";

    /// <summary>Endpoints marked <c>.Unpublished()</c>, samples included. In Swagger, but not in the contract.</summary>
    public const string Unpublished = "unpublished";
}
