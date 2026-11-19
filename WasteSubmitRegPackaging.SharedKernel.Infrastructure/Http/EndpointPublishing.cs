using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;

/// <summary>
/// Marks an endpoint as not published yet. It still works, and Swagger shows it (in the <c>unpublished</c> document),
/// but it's left out of the <c>v1</c> OpenAPI document: the contract that's merged, and that frontends generate
/// clients from. Publishing an endpoint means removing the marker; see the README for what that involves.
/// </summary>
public sealed record UnpublishedEndpointMetadata;

public static class EndpointPublishingExtensions
{
    /// <summary>
    /// Leaves the endpoint (or every endpoint in the group) out of the published v1 OpenAPI document. Remove this
    /// call to publish it.
    /// </summary>
    public static TBuilder Unpublished<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new UnpublishedEndpointMetadata());

    /// <summary>
    /// Marks every endpoint in the group as a sample: unpublished, tagged "<paramref name="tag"/> (sample)" and given
    /// the SAMPLE description. Samples show the structure and will change.
    /// </summary>
    /// <param name="area">What will be developed, in the description, for example "registration".</param>
    public static RouteGroupBuilder AsSample(this RouteGroupBuilder group, string tag, string area) =>
        group
            .WithTags($"{tag} (sample)")
            .WithDescription(
                $"SAMPLE: shows the structure of the {area} API. It will change as the {area} work is developed; don't build against it yet.")
            .Unpublished();
}
