using System;
using System.Collections.Generic;

using Altinn.Platform.Events.Models;

using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Events.Swagger
{
    /// <summary>
    /// Filter for adding examples to the relevant request bodies to enrich open api spec.
    /// </summary>
    public class RequestBodyExampleFilter : IRequestBodyFilter
    {
        /// <inheritdoc/>
        public void Apply(OpenApiRequestBody requestBody, RequestBodyFilterContext context)
        {
            switch (context.BodyParameterDescription.Type.Name)
            {
                case nameof(CloudEventRequestModel):
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
                 new OpenApiObject
                {
                    ["source"] = new OpenApiString("https://ttd.apps.altinn.no/ttd/apps-test/instances/50015641/a72223a3-926b-4095-a2a6-bacc10815f2d"),
                    ["specversion"] = new OpenApiString("1.0"),
                    ["type"] = new OpenApiString("app.instance.created"),
                    ["subject"] = new OpenApiString("/party/50015677"),
                }),
                ("Instance crated event without alternative subject",
                 new OpenApiObject
                {
                    ["source"] = new OpenApiString("https://ttd.apps.altinn.no/ttd/apps-test/instances/50067592/f3c92d96-0eb3-4532-a16f-bcafd94bde3a"),
                    ["specversion"] = new OpenApiString("1.0"),
                    ["type"] = new OpenApiString("app.instance.created"),
                    ["subject"] = new OpenApiString("/party/50067592"),
                })
            };

            Dictionary<string, OpenApiExample> exampleDict = new();

            examples.ForEach(entry => appJson.Examples.Add(entry.Name, new OpenApiExample { Value = entry.Value }));

            requestBody.Content["application/json"] = appJson;
        }

        private static void CreateSubscriptionRequestModelExamples(OpenApiRequestBody requestBody)
        {
            OpenApiMediaType appJson = requestBody.Content["application/json"];

            List<(string Name, OpenApiObject Value)> examples = new()
            {
                ("End user (system) subscribing to events regarding themselves",
                 new OpenApiObject
                {
                    ["endpoint"] = new OpenApiString("https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                    ["sourceFilter"] = new OpenApiString("https://skd.apps.altinn.no/skd/mva-melding"),
                    ["alternativeSubjectFilter"] = new OpenApiString("/person/01017512345"),
                    ["typeFilter"] = new OpenApiString("app.instance.process.completed"),
                }),
                ("Org subscription to events of all their apps",
                new OpenApiObject
                {
                    ["endpoint"] = new OpenApiString("https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                    ["sourceFilter"] = new OpenApiString("https://ttd.apps.altinn.no/ttd/%25"),
                    ["typeFilter"] = new OpenApiString("app.instance.process.completed")
                }),
                ("Org subscription to events with wildcard `_` in source",
                 new OpenApiObject
                {
                    ["endpoint"] = new OpenApiString("https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                    ["sourceFilter"] = new OpenApiString("https://ttd.apps.altinn.no/ttd/apps-test_"),
                }),
                ("Org subscription with wildcard `%25` in source for unknown string",
                 new OpenApiObject
                {
                    ["endpoint"] = new OpenApiString("https://org-reception-func.azurewebsites.net/api/processCompleteInstance?code=APIKEY"),
                    ["sourceFilter"] = new OpenApiString("https://ttd.apps.altinn.no/ttd/versionedapp_v%25"),
                }),

                ("Subscription with Slack webhook",
                 new OpenApiObject
                {
                    ["endpoint"] = new OpenApiString("https://hooks.slack.com/services/TSRSASBVNF3/ADRRSDSSSAahttsasdfasFO3w83456ss"),
                    ["sourceFilter"] = new OpenApiString("https://ttd.apps.altinn.no/ttd/apps-test")
                })
            };

            Dictionary<string, OpenApiExample> exampleDict = new();

            examples.ForEach(entry => appJson.Examples.Add(entry.Name, new OpenApiExample { Value = entry.Value }));

            requestBody.Content["application/json"] = appJson;
        }
    }
}
