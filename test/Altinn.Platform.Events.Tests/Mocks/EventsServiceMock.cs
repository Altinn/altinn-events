using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Models;

using CloudNative.CloudEvents;

namespace Altinn.Platform.Events.Tests.Mocks
{
    public class EventsServiceMock : IEventsService
    {
        private readonly int _eventsCollection;
        private readonly Dictionary<string, int> _partyLookup;
        private readonly JsonSerializerOptions _serializerOptions;

        public EventsServiceMock(int eventsCollection = 1)
        {
            _eventsCollection = eventsCollection;
            _partyLookup = new()
            {
                { "897069650", 500000 },
                { "897069651", 500001 },
                { "897069652", 500002 },
                { "897069653", 500003 },
                { "897069631", 1002 },
                { "01039012345", 1337 },
                { "12345678901",  1000 }
            };

            _serializerOptions = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public Task<List<CloudEvent>> GetAppEvents(string after, DateTime? from, DateTime? to, int partyId, List<string> source, string resource, List<string> type, string unit, string person, int size = 50)
        {
            if (partyId <= 0)
            {
                partyId = PartyLookup(unit, person);
            }

            string eventsPath = Path.Combine(GetEventsPath(), $@"{_eventsCollection}.json");

            if (File.Exists(eventsPath))
            {
                string content = File.ReadAllText(eventsPath);
                List<EventsTableEntry> tableEntries = JsonSerializer.Deserialize<List<EventsTableEntry>>(content, _serializerOptions);

                // logic for filtering on source and type not implemented.
                // source filtering is only enabled for 1 source list element.
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

                if (partyId > 0)
                {
                    string subject = $"/party/{partyId}";
                    filter = filter.Where(te => te.Subject.Equals(subject));
                }

                if (source?.Count == 1)
                {
                    string pattern = "^" + Regex.Escape(source[0]).Replace("%", "*").Replace("_", ".");
                    filter = filter.Where(te => Regex.IsMatch(te.Source.ToString(), pattern));
                }

                if (!string.IsNullOrEmpty(resource))
                {
                    filter = filter.Where(te => te.Resource.Equals(resource));
                }

                List<CloudEvent> result = filter.Select(t => t.CloudEvent)
                    .Take(size)
                    .ToList();

                result.ForEach(ce => ce.Time = ce.Time.Value.ToUniversalTime());
                return Task.FromResult(result);
            }

            return null;
        }

        public Task<List<CloudEvent>> GetEvents(string resource, string after, string subject, string alternativeSubject, List<string> type, int size)
        {
            string eventsPath = Path.Combine(GetEventsPath(), $@"{_eventsCollection}.json");

            if (File.Exists(eventsPath))
            {
                string content = File.ReadAllText(eventsPath);
                List<EventsTableEntry> tableEntries = JsonSerializer.Deserialize<List<EventsTableEntry>>(content, _serializerOptions);

                // logic for filtering on source and type not implemented.
                // source filtering is only enabled for 1 source list element.
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

                if (!string.IsNullOrEmpty(alternativeSubject))
                {
                    filter = filter.Where(te => alternativeSubject.Equals(te.AlternativeSubject));
                }

                if (!string.IsNullOrEmpty(resource))
                {
                    filter = filter.Where(te => resource.Equals(te.Resource));
                }

                List<CloudEvent> result = filter.Select(t => t.CloudEvent)
                    .Take(size)
                    .ToList();

                result.ForEach(ce => ce.Time = ce.Time.Value.ToUniversalTime());
                return Task.FromResult(result);
            }

            return null;
        }

        public Task<string> RegisterNew(CloudEvent cloudEvent)
        {
            throw new NotImplementedException();
        }

        public Task<string> PostInbound(CloudEvent cloudEvent)
        {
            throw new NotImplementedException();
        }

        public Task<string> Save(CloudEvent cloudEvent)
        {
            throw new NotImplementedException();
        }

        private int PartyLookup(string unit, string person)
        {
            return string.IsNullOrEmpty(unit) ? _partyLookup.GetValueOrDefault(person) : _partyLookup.GetValueOrDefault(unit);
        }

        private static string GetEventsPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(EventsServiceMock).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, "..", "..", "..", "Data", "events");
        }
    }
}
