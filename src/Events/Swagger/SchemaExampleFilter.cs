using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

using Altinn.Platform.Events.Models;

using CloudNative.CloudEvents;

using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Events.Swagger
{
    /// <summary>
    /// Filter for adding examples to the various classes to enrich open api spec.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SchemaExampleFilter : ISchemaFilter
    {
        private const string _testUserPrefix = "/user/12345";

        /// <inheritdoc/>
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (schema is OpenApiSchema openApiSchema)
            {
                openApiSchema.Example = GetExampleOrNullFor(context.Type);
            }
        }

        private static JsonNode GetExampleOrNullFor(Type type)
        {
            return type.Name switch
            {
                nameof(CloudEvent) => new JsonObject
                {
                    ["id"] = Guid.NewGuid().ToString(),
                    ["resource"] = "urn:altinn:resource:app_ttd_apps-test",
                    ["source"] = "https://ttd.apps.altinn.no/ttd/apps-test/instances/50015641/a72223a3-926b-4095-a2a6-bacc10815f2d",
                    ["specversion"] = "1.0",
                    ["type"] = "app.instance.created",
                    ["subject"] = "/party/50015641",
                    ["alternativesubject"] = "/person/27124902369",
                    ["time"] = "2020-10-29T07:22:19.438039Z"
                },
                nameof(LogEntryDto) => new JsonObject
                {
                    ["cloudEventId"] = Guid.NewGuid().ToString(),
                    ["cloudEventType"] = "app.instance.created",
                    ["cloudEventResource"] = "urn:altinn:resource:app_ttd_apps-test",
                    ["subscriptionId"] = 1,
                    ["consumer"] = _testUserPrefix,
                    ["endpoint"] = "https://enduser-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY",
                    ["statusCode"] = 200
                },
                nameof(Subscription) => new JsonObject
                {
                    ["endPoint"] = "https://enduser-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY",
                    ["id"] = 1,
                    ["sourceFilter"] = "https://skd.apps.altinn.cloud/skd/mva-melding",
                    ["subjectFilter"] = "/party/512345",
                    ["typeFilter"] = "app.instance.process.completed",
                    ["consumer"] = _testUserPrefix,
                    ["createdBy"] = _testUserPrefix,
                    ["created"] = "2022-07-27T13:14:14.395226Z"
                },
                nameof(SubscriptionList) => new JsonObject
                {
                    ["count"] = 2,
                    ["subscriptions"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["endPoint"] = "https://enduser-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY",
                                ["id"] = 1,
                                ["sourceFilter"] = "https://skd.apps.altinn.cloud/skd/mva-melding",
                                ["subjectFilter"] = "/party/512345",
                                ["typeFilter"] = "app.instance.process.completed",
                                ["consumer"] = _testUserPrefix,
                                ["createdBy"] = _testUserPrefix,
                                ["created"] = "2022-07-27T13:14:14.395226Z"
                            },
                            new JsonObject
                            {
                                ["endPoint"] = "https://hooks.slack.com/services/ID/CODE",
                                ["id"] = 2,
                                ["sourceFilter"] = "https://ttd.apps.altinn.cloud/ttd/apps-test",
                                ["consumer"] = "/org/ttd",
                                ["createdBy"] = "/org/ttd5",
                                ["created"] = "2022-08-02T08:49:07.269958Z"
                            }
                        }
                },
                _ => null,
            };
        }
    }
}
