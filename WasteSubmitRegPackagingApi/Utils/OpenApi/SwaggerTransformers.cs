using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;
using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackagingApi.Utils.OpenApi;

/// <summary>
/// Tidies the OpenAPI document for clients and Swagger UI: endpoints that need a user get the X-User-* header
/// fields for their kind of user, filled in with example values.
/// </summary>
internal static class SwaggerTransformers
{
    public static OpenApiOptions AddSwaggerTransformers(this OpenApiOptions options)
    {
        options.CreateSchemaReferenceId = SchemaIds.ForDocument();

        options.AddDocumentTransformer((document, _, _) =>
        {
            // Request bodies only: there, "required" comes from [Required], so the value can't be null. Response
            // records list every property as required, including ones that can be null, so they are left alone.
            // This runs on the finished document, because references to enum schemas are only in place by then.
            var requestBodySchemaIds = ((IEnumerable<IOpenApiPathItem>?)document.Paths?.Values ?? [])
                .SelectMany(path => (IEnumerable<OpenApiOperation>?)path.Operations?.Values ?? [])
                .SelectMany(operation => (IEnumerable<OpenApiMediaType>?)operation.RequestBody?.Content?.Values ?? [])
                .Select(media => (media.Schema as OpenApiSchemaReference)?.Reference.Id)
                .OfType<string>()
                .ToHashSet();

            foreach (var id in requestBodySchemaIds)
            {
                if (document.Components?.Schemas is { } schemas && schemas.TryGetValue(id, out var found) && found is OpenApiSchema requestSchema)
                {
                    RequiredIsNeverNull(requestSchema);
                }
            }

            return Task.CompletedTask;
        });

        options.AddSchemaTransformer((schema, _, _) =>
        {
            TidyForClients(schema);
            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, _) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;

            switch (metadata.OfType<RequiresUserMetadata>().FirstOrDefault()?.UserType)
            {
                case UserType.Producer:
                    operation.Parameters ??= [];
                    operation.Parameters.Add(Header(UserHeaders.UserId, "The producer user the caller is acting for.",
                        format: "uuid", example: "22222222-2222-2222-2222-222222222222"));
                    operation.Parameters.Add(Header(UserHeaders.Type, "Always Producer here.", example: "Producer", allowed: ["Producer"]));
                    operation.Parameters.Add(Header(UserHeaders.OrganisationId, "The organisation the user acts for.",
                        format: "uuid", example: "11111111-1111-1111-1111-111111111111"));
                    break;
                case UserType.Regulator:
                    operation.Parameters ??= [];
                    operation.Parameters.Add(Header(UserHeaders.UserId, "The regulator user the caller is acting for.",
                        format: "uuid", example: "33333333-3333-3333-3333-333333333333"));
                    operation.Parameters.Add(Header(UserHeaders.Type, "Always Regulator here.", example: "Regulator", allowed: ["Regulator"]));
                    break;
            }

            return Task.CompletedTask;
        });

        return options;
    }

    /// <summary>
    /// Makes the generated schemas say what the API accepts, so Swagger UI builds a valid example body.
    /// <list type="bullet">
    /// <item>Enums are strings (JsonStringEnumConverter). Say so, and drop the null value a nullable enum adds.</item>
    /// <item>Integers are generated as "integer or numeric string" (the web JSON defaults accept both). Advertise
    /// integer only.</item>
    /// </list>
    /// </summary>
    private static void TidyForClients(OpenApiSchema schema)
    {
        if (schema.Enum is { Count: > 0 } values)
        {
            var named = values.Where(value => value is not null).ToList();
            if (named.Count != values.Count)
            {
                schema.Enum = named;
            }

            if (named.All(value => value is JsonValue json && json.GetValueKind() == System.Text.Json.JsonValueKind.String))
            {
                schema.Type = JsonSchemaType.String;
            }
        }

        if (schema.Type is { } type && type.HasFlag(JsonSchemaType.Integer) && type.HasFlag(JsonSchemaType.String))
        {
            schema.Type = type & ~JsonSchemaType.String;
            schema.Pattern = null;
        }
    }

    /// <summary>
    /// Request value types are nullable only so that a missing field fails [Required]. A required request
    /// property is therefore never null, so the null option is dropped.
    /// </summary>
    private static void RequiredIsNeverNull(OpenApiSchema schema)
    {
        if (schema.Properties is null || schema.Required is null)
            return;

        foreach (var name in schema.Required)
        {
            if (!schema.Properties.TryGetValue(name, out var property))
                continue;

            if (property.OneOf is { Count: 2 } options && options.FirstOrDefault(option => option.Type == JsonSchemaType.Null) is { } nullOption)
            {
                schema.Properties[name] = options.Single(option => !ReferenceEquals(option, nullOption));
            }
            else if (property is OpenApiSchema { Type: { } propertyType } inline && propertyType.HasFlag(JsonSchemaType.Null))
            {
                inline.Type = propertyType & ~JsonSchemaType.Null;
            }
        }
    }

    private static OpenApiParameter Header(
        string name, string description, string? format = null, string? example = null, string[]? allowed = null) =>
        new()
        {
            Name = name,
            In = ParameterLocation.Header,
            Required = true,
            Description = description,
            Example = example is null ? null : JsonValue.Create(example),
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = format,
                Enum = allowed?.Select(value => (JsonNode)JsonValue.Create(value)).ToList()
            }
        };
}
