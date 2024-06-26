using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Tests.Models;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Tests.Mocks
{
    /// <summary>
    /// Class that mocks storing and retrieving documents from postgres DB.
    /// </summary>
    public class CloudEventRepositoryMock : ICloudEventRepository
    {
        private readonly int _eventsCollection;

        public CloudEventRepositoryMock(int eventsCollection = 1)
        {
            _eventsCollection = eventsCollection;
        }

        /// <inheritdoc/>
        public Task CreateEvent(string cloudEvent)
        {
            return Task.CompletedTask;
        }

        private static EventsTableEntry NewTestEvent(
            int sequenceno,
            string id,
            Uri source,
            string resource,
            string type,
            string subject,
            string alternativesubject,
            string time,
            object data = null)
        {
            var e = new CloudEvent(CloudEventsSpecVersion.V1_0);
            e["id"] = id;
            e["source"] = source;
            e["resource"] = resource;
            e["type"] = type;
            e["subject"] = subject;
            e["alternativesubject"] = alternativesubject;
            e["time"] = DateTimeOffset.Parse(time);
            e.Data = data;

            var ete = new EventsTableEntry()
            {
                SequenceNo = sequenceno,
                Id = id,
                Source = source,
                Resource = resource,
                Subject = subject,
                Time = DateTime.Parse(time),
                Type = type,
                CloudEvent = e
            };
            return ete;
        }

        private List<EventsTableEntry> GetTestEvents(int eventsCollectionNumber)
        {
            var events = new List<EventsTableEntry>();
            if (eventsCollectionNumber == 1)
            {
                events.Add(NewTestEvent(
                    1,
                    "e31dbb11-2208-4dda-a549-92a0db8c7708",
                    new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/6fb3f738-6800-4f29-9f3e-1c66862656cd"),
                    "urn:altinn:resource:app_ttd_endring-av-navn-v2",
                    "instance.deleted",
                    "/party/1337",
                    "/person/01038712345",
                    "2020-10-13T11:50:29Z"));

                events.Add(NewTestEvent(
                    2,
                    "e31dbb11-2208-4dda-a549-92a0db8c7708",
                    new Uri("https://ttd.apps.altinn.no/ttd/endring-av-navn-v2/instances/1337/6fb3f738-6800-4f29-9f3e-1c66862656cd"),
                    "urn:altinn:resource:app_ttd_endring-av-navn-v2",
                    "instance.deleted",
                    "/party/1337",
                    "/person/01038712345",
                    "2020-10-13T12:50:29Z"));
            }

            if (_eventsCollection == 2)
            {
                events.Add(NewTestEvent(
                    1,
                    "e31dbb11-2208-4dda-a549-92a0db8c7708",
                    new Uri("https://nav.apps.altinn.no/nav/app/instances/1337/e31dbb11-2208-4dda-a549-92a0db8c7708"),
                    "urn:altinn:resource:app_nav_app",
                    "instance.created",
                    "/party/1337",
                    "/person/01038712345",
                    "2020-06-06T11:50:29.463221Z",
                    "data field"));

                events.Add(NewTestEvent(
                    2,
                    "e31dbb11-2208-4dda-a549-92a0db8c8808",
                    new Uri("https://nav.apps.altinn.no/nav/app/instances/1337/e31dbb11-2208-4dda-a549-92a0db8c8808"),
                    "urn:altinn:resource:app_nav_app",
                    "instance.created",
                    "/party/1337",
                    "/person/01038712345",
                    "2020-06-06T11:50:29.463221Z",
                    "data field"));

                events.Add(NewTestEvent(
                    3,
                    "e31dbb11-2208-4dda-a549-92a0db8c9908",
                    new Uri("https://nav.apps.altinn.no/nav/app/instances/12345/e31dbb11-2208-4dda-a549-92a0db8c9908"),
                    "urn:altinn:resource:app_nav_app",
                    "instance.restored",
                    "/party/12345",
                    "/person/01038712345",
                    "2020-06-16T12:51:29.463221Z",
                    "data field"));

                events.Add(NewTestEvent(
                    4,
                    "e31dbb11-2208-4dda-a549-92a0db8c0008",
                    new Uri("https://nav.apps.altinn.no/nav/app/instances/12345/e31dbb11-2208-4dda-a549-92a0db8c0008"),
                    "urn:altinn:resource:app_nav_app",
                    "instance.saved",
                    "/party/54321",
                    "/person/01038754321",
                    "2020-06-17T08:50:29.463221Z",
                    "data field"));

                events.Add(NewTestEvent(
                    5,
                    "e31dbb11-2208-4dda-a549-92a0db8c2208",
                    new Uri("https://skd.apps.altinn.no/skd/sirius/instances/54321/e31dbb11-2208-4dda-a549-92a0db8c9908"),
                    "urn:altinn:resource:app_skd_sirius",
                    "instance.saved",
                    "/party/54321",
                    "/person/01038754321",
                    "2020-06-18T12:51:29.463221Z",
                    "data field"));
            }

            return events;
        }

        /// <inheritdoc/>
        public Task<List<CloudEvent>> GetAppEvents(string after, DateTime? from, DateTime? to, string subject, List<string> source, string resource, List<string> type, int size)
        {
            var tableEntries = GetTestEvents(_eventsCollection);

            // logic for filtering on source and type not implemented.
            IEnumerable<EventsTableEntry> filter = tableEntries;

            if (!string.IsNullOrEmpty(after))
            {
                int sequenceNo = filter.Where(te => te.Id.Equals(after)).Select(te => te.SequenceNo).FirstOrDefault();
                filter = filter.Where(te => te.SequenceNo > sequenceNo);
            }

            if (from.HasValue)
            {
                filter = filter.Where(te => te.Time >= from);
            }

            if (to.HasValue)
            {
                filter = filter.Where(te => te.Time <= to);
            }

            if (!string.IsNullOrEmpty(subject))
            {
                filter = filter.Where(te => te.Subject.Equals(subject));
            }

            if (source != null && source.Count > 0)
            {
                // requires more logic to match all fancy cases.
                filter = filter.Where(te => source.Contains(te.Source.ToString()));
            }

            if (type != null && type.Count > 0)
            {
                // requires more logic to match all fancy cases.
                filter = filter.Where(te => type.Contains(te.Type.ToString()));
            }

            if (!string.IsNullOrEmpty(resource))
            {
                filter = filter.Where(te => te.Resource.Equals(resource));
            }

            List<CloudEvent> result = filter.Select(t => t.CloudEvent)
                .Take(size)
                .ToList();

            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task<List<CloudEvent>> GetEvents(string resource, string after, string subject, string alternativeSubject, List<string> type, int size)
        {
            var tableEntries = GetTestEvents(_eventsCollection);

            // logic for filtering on source and type not implemented.
            IEnumerable<EventsTableEntry> filter = tableEntries;

            if (!string.IsNullOrEmpty(after))
            {
                int sequenceNo = filter.Where(te => te.Id.Equals(after)).Select(te => te.SequenceNo).FirstOrDefault();
                filter = filter.Where(te => te.SequenceNo > sequenceNo);
            }

            if (!string.IsNullOrEmpty(subject))
            {
                filter = filter.Where(te => te.Subject.Equals(subject));
            }

            if (!string.IsNullOrEmpty(resource))
            {
                filter = filter.Where(te => te.Resource == resource);
            }

            if (type != null && type.Count > 0)
            {
                // requires more logic to match all fancy cases.
                filter = filter.Where(te => type.Contains(te.Type.ToString()));
            }

            List<CloudEvent> result = filter.Select(t => t.CloudEvent)
                .Take(size)
                .ToList();

            return Task.FromResult(result);
        }

        private static string GetEventsPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(EventsServiceMock).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, "..", "..", "..", "Data", "events");
        }
    }
}
