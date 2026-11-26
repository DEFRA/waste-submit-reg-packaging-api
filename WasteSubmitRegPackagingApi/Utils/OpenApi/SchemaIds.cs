using System.Collections.Concurrent;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;

namespace WasteSubmitRegPackagingApi.Utils.OpenApi;

/// <summary>
/// The schema names in the OpenAPI documents. By default a schema is named after its C# type's short name, so two
/// types with the same name (in different modules) would share one schema, and one would silently replace the other.
/// Generated clients would then get the wrong type: this happened with the two <c>SubmitterType</c> enums.
/// <list type="bullet">
/// <item>A clash fails the build (the build writes the documents) and the tests, instead of being silent.</item>
/// <item>Resolve a clash in <see cref="s_renamed"/> by prefixing the newer type with its module name.</item>
/// </list>
/// </summary>
internal static class SchemaIds
{
    private static readonly Dictionary<Type, string> s_renamed = new()
    {
        // Manage Obligations' SubmitterType (DirectRegistrant, ComplianceScheme) is an existing contract, so it keeps
        // the plain name.
        [typeof(WasteSubmitRegPackaging.SubmissionPeriods.Contracts.SubmitterType)] = "SubmissionPeriodsSubmitterType",
    };

    /// <summary>A naming function for one document. Each document gets its own, so each tracks its own names.</summary>
    public static Func<JsonTypeInfo, string?> ForDocument()
    {
        var owners = new ConcurrentDictionary<string, Type>();

        return typeInfo =>
        {
            var type = Nullable.GetUnderlyingType(typeInfo.Type) ?? typeInfo.Type;
            var id = s_renamed.TryGetValue(type, out var renamed)
                ? renamed
                : OpenApiOptions.CreateDefaultSchemaReferenceId(typeInfo);

            if (id is not null && owners.GetOrAdd(id, type) is var owner && owner != type)
            {
                throw new InvalidOperationException(
                    $"OpenAPI schema name '{id}' is used by both {owner.FullName} and {type.FullName}. " +
                    $"Give one of them its own name in {nameof(SchemaIds)}.");
            }

            return id;
        };
    }
}
