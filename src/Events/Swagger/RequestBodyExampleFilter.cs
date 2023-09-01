using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Altinn.Platform.Events.Models;

using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Events.Swagger
{
    /// <summary>
    /// Filter for adding examples to the relevant request bodies to enrich open api spec.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class RequestBodyExampleFilter : IRequestBodyFilter
    {
        /// <inheritdoc/>
        public void Apply(OpenApiRequestBody requestBody, RequestBodyFilterContext context)
        {
            switch (context.BodyParameterDescription.Type.Name)
            {
                case nameof(AppCloudEventRequestModel):
                    CreateCloudEventRequestModelExamples(requestBody);
                    return;
                case nameof(SubscriptionRequestModel):
                    CreateSubscriptionRequestModelExamples(requestBody);
                    return;
                default:
                    return;
            }
        }

        private static void CreateCloudEventRequestModelExamples(OpenApiRequestBody requestBody)
        {
            OpenApiMediaType appJson = requestBody.Content["application/json"];

            List<(string Name, OpenApiObject Value)> examples = new()
            {
                ("Instance created event with alternative subject",
                CreateOpenApiObject(new List<(string Name, string Value)>()
                {
                    ("resource", "urn:altinn:resource:altinnapp.ttd.apps-test"),
                    ("resourceinstance", "50015641/a72223a3-926b-4095-a2a6-bacc10815f2d"),
                    ("source", "https://ttd.apps.altinn.no/ttd/apps-test/instances/50015641/a72223a3-926b-4095-a2a6-bacc10815f2d"),
                    ("specversion",  "1.0"),
                    ("type",  "app.instance.created"),
                    ("subject",  "/party/50015641"),
                    ("alternativesubject", "/person/01017512345")
                })),
                ("Instance created event without alternative subject",
                CreateOpenApiObject(new List<(string Name, string Value)>()
                {
                    ("resource", "urn:altinn:resource:altinnapp.ttd.apps-test"),
                    ("resourceinstance", "50067592/f3c92d96-0eb3-4532-a16f-bcafd94bde3a"),
                    ("source", "https://ttd.apps.altinn.no/ttd/apps-test/instances/50067592/f3c92d96-0eb3-4532-a16f-bcafd94bde3a"),
                    ("specversion",  "1.0"),
                    ("type",  "app.instance.created"),
                    ("subject",  "/party/50067592")
                }))
            };

            examples.ForEach(entry => appJson.Examples.Add(entry.Name, new OpenApiExample { Value = entry.Value }));

            requestBody.Content["application/json"] = appJson;
        }

        private static void CreateSubscriptionRequestModelExamples(OpenApiRequestBody requestBody)
        {
            OpenApiMediaType appJson = requestBody.Content["application/json"];

            List<(string Name, OpenApiObject Value)> examples = new()
            {
                 ("End user (system) subscribing to events regarding themselves",
                 CreateOpenApiObject(new List<(string Name, string Value)>()
                 {
                    ("endpoint", "https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                    ("sourceFilter", "https://skd.apps.altinn.no/skd/mva-melding"),
                    ("alternativeSubjectFilter", "/person/01017512345"),
                    ("typeFilter", "app.instance.process.completed")
                 })),
                 ("End user (system) subscribing to events from a specific regarding an organisation",
                 CreateOpenApiObject(new List<(string Name, string Value)>()
                 {
                    ("endpoint", "https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                    ("sourceFilter", "https://skd.apps.altinn.no/skd/mva-melding"),
                    ("alternativeSubjectFilter", "/org/897069651"),
                    ("typeFilter", "app.instance.process.completed")
                 })),
                 ("Org subscription to events of all their apps",
                 CreateOpenApiObject(new List<(string Name, string Value)>()
                 {
                    ("endpoint", "https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                    ("sourceFilter", "https://ttd.apps.altinn.no/ttd/%25"),
                    ("typeFilter", "app.instance.process.completed")
                 })),

                 ("Subscription with Slack webhook",
                 CreateOpenApiObject(new List<(string Name, string Value)>()
                 {
                     ("endpoint", "https://hooks.slack.com/services/TSRSASBVNF3/ADRRSDSSSAahttsasdfasFO3w83456ss"),
                     ("resourceFilter", "urn:altinn:resource:altinnapp.ttd.apps-test")
                 }))
            };

            examples.ForEach(entry => appJson.Examples.Add(entry.Name, new OpenApiExample { Value = entry.Value }));

            requestBody.Content["application/json"] = appJson;
        }

        private static OpenApiObject CreateOpenApiObject(List<(string Name, string Value)> elements)
        {
            var obj = new OpenApiObject();
            elements.ForEach(e => obj.Add(e.Name, new OpenApiString(e.Value)));

            return obj;
        }
    }
}
