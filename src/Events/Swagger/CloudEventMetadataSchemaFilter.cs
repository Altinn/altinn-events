using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Nodes;

using CloudNative.CloudEvents;

using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Events.Swagger;

/// <summary>
/// Removes SDK-internal metadata properties from the <see cref="CloudEvent"/> schema and documents
/// the CloudEvents extension attributes that our API populates and exposes on the wire.
/// <see cref="CloudEvent.ExtensionAttributes"/> and <see cref="CloudEvent.IsValid"/> describe attribute
/// definitions and validation state used by the CloudEvents SDK itself; they are never populated by our
/// custom serialization (<c>CloudEventJsonOutputFormatter</c>/<c>CloudEventJsonInputFormatter</c>) and
/// therefore have no bearing on the actual request/response payloads.
/// Used together with the <see cref="CloudEventAttribute"/> type mapping in Program.cs, which stops
/// Swashbuckle from recursively generating schemas for the entire System.Reflection type graph (Type,
/// Assembly, Module, MethodInfo, ...) reachable via <c>CloudEventAttributeType.ClrType</c>.
/// </summary>
[ExcludeFromCodeCoverage]
public class CloudEventMetadataSchemaFilter : ISchemaFilter
{
    private static readonly string[] _propertiesToRemove = ["extensionAttributes", "isValid"];

    /// <summary>
    /// CloudEvents extension attributes that <see cref="CloudEvent"/> exposes only via its string indexer
    /// (e.g. <c>cloudEvent["resource"]</c>) and that Swashbuckle therefore cannot discover through reflection.
    /// They are added explicitly here so consumers can see their shape and meaning in the generated spec.
    /// </summary>
    private static readonly (string Name, string Description, string Example)[] _extensionPropertiesToAdd =
    [
        ("resource", "URN uniquely identifying the resource the event relates to.", "urn:altinn:resource:app_ttd_apps-test"),
        ("resourceinstance", "Identifier of the specific instance of the resource the event relates to.", "50015641/a72223a3-926b-4095-a2a6-bacc10815f2d"),
        ("alternativesubject", "Alternative identifier for the subject of the event, e.g. a person or organisation number, used when the subject cannot be expressed as a party id.", "/person/27124902369")
    ];

    /// <inheritdoc/>
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(CloudEvent) || schema is not OpenApiSchema openApiSchema || openApiSchema.Properties is null)
        {
            return;
        }

        // Swashbuckle defaults object schemas to "additionalProperties: false", i.e. closed for extension.
        // CloudEvent carries arbitrary CloudEvents extension attributes beyond the ones we document
        // explicitly below, so the schema must stay open to reflect that reality.
        openApiSchema.AdditionalPropertiesAllowed = true;

        foreach (string propertyName in _propertiesToRemove)
        {
            string key = openApiSchema.Properties.Keys
                .FirstOrDefault(k => string.Equals(k, propertyName, System.StringComparison.OrdinalIgnoreCase));

            if (key is not null)
            {
                openApiSchema.Properties.Remove(key);
            }
        }

        foreach ((string name, string description, string example) in _extensionPropertiesToAdd)
        {
            if (openApiSchema.Properties.ContainsKey(name))
            {
                continue;
            }

            openApiSchema.Properties[name] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null,
                Description = description,
                Example = JsonValue.Create(example)
            };
        }
    }
}
