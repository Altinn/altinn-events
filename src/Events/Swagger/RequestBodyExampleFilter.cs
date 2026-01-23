using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

using Altinn.Platform.Events.Models;

using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Events.Swagger
{
    /// <summary>
    /// Filter for adding examples to the relevant request bodies to enrich open api spec.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class RequestBodyExampleFilter : IRequestBodyFilter
    {
        private const string _applicationJson = "application/json";

        /// <inheritdoc/>
        public void Apply(IOpenApiRequestBody requestBody, RequestBodyFilterContext context)
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

        private static void CreateCloudEventRequestModelExamples(IOpenApiRequestBody requestBody)
        {
            if (requestBody.Content == null || !requestBody.Content.TryGetValue(_applicationJson, out OpenApiMediaType appJson))
            {
                return;
            }

            appJson.Examples ??= new Dictionary<string, IOpenApiExample>();

            List<(string Name, JsonObject Value)> examples =
            [
                ("Instance created event with alternative subject",
                CreateOpenApiObject(
                [
                    ("resource", "urn:altinn:resource:app_ttd_apps-test"),
                    ("resourceinstance", "50015641/a72223a3-926b-4095-a2a6-bacc10815f2d"),
                    ("source", "https://ttd.apps.altinn.no/ttd/apps-test/instances/50015641/a72223a3-926b-4095-a2a6-bacc10815f2d"),
                    ("specversion",  "1.0"),
                    ("type",  "app.instance.created"),
                    ("subject",  "/party/50015641"),
                    ("alternativesubject", "/person/01017512345")
                ])),
                ("Instance created event without alternative subject",
                CreateOpenApiObject(
                [
                    ("resource", "urn:altinn:resource:app_ttd_apps-test"),
                    ("resourceinstance", "50067592/f3c92d96-0eb3-4532-a16f-bcafd94bde3a"),
                    ("source", "https://ttd.apps.altinn.no/ttd/apps-test/instances/50067592/f3c92d96-0eb3-4532-a16f-bcafd94bde3a"),
                    ("specversion",  "1.0"),
                    ("type",  "app.instance.created"),
                    ("subject",  "/party/50067592")
                ]))
            ];

            foreach (var (name, value) in examples)
            {
                appJson.Examples[name] = new OpenApiExample { Value = value };
            }

            requestBody.Content[_applicationJson] = appJson;
        }

        private static void CreateSubscriptionRequestModelExamples(IOpenApiRequestBody requestBody)
        {
            if (requestBody.Content == null || !requestBody.Content.TryGetValue(_applicationJson, out OpenApiMediaType appJson))
            {
                return;
            }

            appJson.Examples ??= new Dictionary<string, IOpenApiExample>();

            List<(string Name, JsonObject Value)> examples =
            [
                 ("End user (system) subscribing to events regarding themselves",
                 CreateOpenApiObject(new List<(string Name, string Value)>()
                 {
                    ("endpoint", "https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                    ("sourceFilter", "https://skd.apps.altinn.no/skd/mva-melding"),
                    ("alternativeSubjectFilter", "/person/01017512345"),
                    ("typeFilter", "app.instance.process.completed")
                 })),
                 ("End user (system) subscribing to events from a specific regarding an organisation",
                 CreateOpenApiObject(
                 [
                    ("endpoint", "https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                    ("sourceFilter", "https://skd.apps.altinn.no/skd/mva-melding"),
                    ("alternativeSubjectFilter", "/organisation/897069651"),
                    ("typeFilter", "app.instance.process.completed")
                 ])),
                 ("Org subscription to events of all their apps",
                 CreateOpenApiObject(
                 [
                    ("endpoint", "https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                    ("sourceFilter", "https://ttd.apps.altinn.no/ttd/%25"),
                    ("typeFilter", "app.instance.process.completed")
                 ])),

                 ("Subscription with Slack webhook",
                 CreateOpenApiObject(
                 [
                     ("endpoint", "https://hooks.slack.com/services/{include-webhook}"),
                     ("resourceFilter", "urn:altinn:resource:app_ttd_apps-test")
                 ]))
            ];

            foreach (var (name, value) in examples)
            {
                appJson.Examples[name] = new OpenApiExample { Value = value };
            }

            requestBody.Content[_applicationJson] = appJson;
        }

        private static JsonObject CreateOpenApiObject(List<(string Name, string Value)> elements)
        {
            var obj = new JsonObject();
            elements.ForEach(e => obj.Add(e.Name, JsonValue.Create(e.Value)));

            return obj;
        }
    }
}
