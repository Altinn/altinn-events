﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Tests.Utils
{
    public static class TestdataUtil
    {
        private static JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static XacmlJsonResponse GetXacmlJsonResponse(string testCase)
        {
            string xacmlResponsePath = Path.Combine(GetXacmlResponsePath(), $@"{testCase}.json");

            if (File.Exists(xacmlResponsePath))
            {
                string content = File.ReadAllText(xacmlResponsePath);
                XacmlJsonResponse response = JsonSerializer.Deserialize<XacmlJsonResponse>(content, _options);
                return response;
            }

            return null;
        }

        public static List<CloudEvent> GetXacmlRequestCloudEventList()
        {
            string eventListPath = Path.Combine(GetXacmlResponsePath(), $@"events.json");

            if (File.Exists(eventListPath))
            {
                string content = File.ReadAllText(eventListPath);
                List<CloudEvent> response = JsonSerializer.Deserialize<List<CloudEvent>>(content, _options);
                return response;
            }

            return null;
        }

        public static CloudEvent Clone(this CloudEvent cloudEvent)
        {
            CloudEvent copy = new CloudEvent(cloudEvent.SpecVersion)
            {
                Id = cloudEvent.Id,
                Data = cloudEvent.Data,
                DataContentType = cloudEvent.DataContentType,
                DataSchema = cloudEvent.DataSchema,
                Source = cloudEvent.Source,
                Subject = cloudEvent.Subject,
                Time = cloudEvent.Time,
                Type = cloudEvent.Type
            };
            if (cloudEvent["resource"] is not null)
            {
                copy["resource"] = cloudEvent["resource"];
            }

            if (cloudEvent["resourceinstance"] is not null)
            {
                copy["resourceinstance"] = cloudEvent["resourceinstance"];
            }

            return copy;
        }

        private static string GetXacmlResponsePath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(TestdataUtil).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, "..", "..", "..", "Data", "xacmlresponses");
        }
    }
}
