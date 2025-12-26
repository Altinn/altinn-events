using System;
using System.Diagnostics.CodeAnalysis;

using Altinn.Platform.Events.Models;

using CloudNative.CloudEvents;

using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

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
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            schema.Example = GetExampleOrNullFor(context.Type);
        }

        private static OpenApiObject GetExampleOrNullFor(Type type)
        {
            return type.Name switch
            {
                nameof(CloudEvent) => new OpenApiObject
                {
                    ["id"] = new OpenApiString(Guid.NewGuid().ToString()),
                    ["resource"] = new OpenApiString("urn:altinn:resource:app_ttd_apps-test"),
                    ["source"] = new OpenApiString("https://ttd.apps.altinn.no/ttd/apps-test/instances/50015641/a72223a3-926b-4095-a2a6-bacc10815f2d"),
                    ["specversion"] = new OpenApiString("1.0"),
                    ["type"] = new OpenApiString("app.instance.created"),
                    ["subject"] = new OpenApiString("/party/50015641"),
                    ["alternativesubject"] = new OpenApiString("/person/27124902369"),
                    ["time"] = new OpenApiString("2020-10-29T07:22:19.438039Z")
                },
                nameof(LogEntryDto) => new OpenApiObject
                {
                    ["cloudEventId"] = new OpenApiString(Guid.NewGuid().ToString()),
                    ["cloudEventType"] = new OpenApiString("app.instance.created"),
                    ["cloudEventResource"] = new OpenApiString("urn:altinn:resource:app_ttd_apps-test"),
                    ["subscriptionId"] = new OpenApiInteger(1),
                    ["consumer"] = new OpenApiString(_testUserPrefix),
                    ["endpoint"] = new OpenApiString("https://enduser-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                    ["statusCode"] = new OpenApiInteger(200)
                },
                nameof(Subscription) => new OpenApiObject
                {
                    ["endPoint"] = new OpenApiString("https://enduser-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                    ["id"] = new OpenApiInteger(1),
                    ["sourceFilter"] = new OpenApiString("https://skd.apps.altinn.cloud/skd/mva-melding"),
                    ["subjectFilter"] = new OpenApiString("/party/512345"),
                    ["typeFilter"] = new OpenApiString("app.instance.process.completed"),
                    ["consumer"] = new OpenApiString(_testUserPrefix),
                    ["createdBy"] = new OpenApiString(_testUserPrefix),
                    ["created"] = new OpenApiString("2022-07-27T13:14:14.395226Z")
                },
                nameof(SubscriptionList) => new OpenApiObject
                {
                    ["count"] = new OpenApiInteger(2),
                    ["subscriptions"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["endPoint"] = new OpenApiString("https://enduser-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                                ["id"] = new OpenApiInteger(1),
                                ["sourceFilter"] = new OpenApiString("https://skd.apps.altinn.cloud/skd/mva-melding"),
                                ["subjectFilter"] = new OpenApiString("/party/512345"),
                                ["typeFilter"] = new OpenApiString("app.instance.process.completed"),
                                ["consumer"] = new OpenApiString(_testUserPrefix),
                                ["createdBy"] = new OpenApiString(_testUserPrefix),
                                ["created"] = new OpenApiString("2022-07-27T13:14:14.395226Z")
                            },
                            new OpenApiObject
                            {
                                ["endPoint"] = new OpenApiString("https://hooks.slack.com/services/ID/CODE"),
                                ["id"] = new OpenApiInteger(2),
                                ["sourceFilter"] = new OpenApiString("https://ttd.apps.altinn.cloud/ttd/apps-test"),
                                ["consumer"] = new OpenApiString("/org/ttd"),
                                ["createdBy"] = new OpenApiString("/org/ttd5"),
                                ["created"] = new OpenApiString("2022-08-02T08:49:07.269958Z")
                            }
                        }
                },
                _ => null,
            };
        }
    }
}
