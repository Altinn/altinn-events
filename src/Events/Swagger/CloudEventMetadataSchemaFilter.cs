using System.Diagnostics.CodeAnalysis;
using System.Linq;

using CloudNative.CloudEvents;

using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Events.Swagger;

/// <summary>
/// Removes SDK-internal metadata properties from the <see cref="CloudEvent"/> schema.
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

    /// <inheritdoc/>
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(CloudEvent) || schema is not OpenApiSchema openApiSchema || openApiSchema.Properties is null)
        {
            return;
        }

        foreach (string propertyName in _propertiesToRemove)
        {
            string key = openApiSchema.Properties.Keys
                .FirstOrDefault(k => string.Equals(k, propertyName, System.StringComparison.OrdinalIgnoreCase));

            if (key is not null)
            {
                openApiSchema.Properties.Remove(key);
            }
        }
    }
}
