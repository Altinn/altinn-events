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
                    CreateExamplesForCloudEventRequestModel(requestBody);
                    return;
                default:
                    return;
            }
        }

        private static void CreateExamplesForCloudEventRequestModel(OpenApiRequestBody requestBody)
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

    }
}
