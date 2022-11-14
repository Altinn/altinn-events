using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Services.Interfaces;
using Altinn.Platform.Events.Tests.Models;

using CloudNative.CloudEvents;

using Newtonsoft.Json;

namespace Altinn.Platform.Events.Tests.Mocks
{
    public class EventsServiceMock : IEventsService
    {
        private readonly int _eventsCollection;
        private readonly Dictionary<string, int> _partyLookup;

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
        }

        public Task<List<CloudEventOld>> GetAppEvents(string after, DateTime? from, DateTime? to, int partyId, List<string> source, List<string> type, string unit, string person, int size)
        {
            if (partyId <= 0)
            {
                partyId = PartyLookup(unit, person);
            }

            string eventsPath = Path.Combine(GetEventsPath(), $@"{_eventsCollection}.json");

            if (File.Exists(eventsPath))
            {
                string content = File.ReadAllText(eventsPath);
                List<EventsTableEntry> tableEntries = JsonConvert.DeserializeObject<List<EventsTableEntry>>(content);

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

                if (source.Count == 1)
                {
                    string pattern = "^" + Regex.Escape(source[0]).Replace("%", "*").Replace("_", ".");
                    filter = filter.Where(te => Regex.IsMatch(te.Source.ToString(), pattern));
                }

                List<CloudEventOld> result = filter.Select(t => t.CloudEvent)
                    .Take(size)
                    .ToList();

                result.ForEach(ce => ce.Time = ce.Time.Value.ToUniversalTime());
                return Task.FromResult(result);
            }

            return null;
        }

        public Task<string> Save(CloudEventOld cloudEvent)
        {
            throw new NotImplementedException();
        }

        public Task<string> RegisterNew(CloudEvent cloudEvent)
        {
            throw new NotImplementedException();
        }

        public Task<string> PostInbound(CloudEvent cloudEvent)
        {
            throw new NotImplementedException();
        }
        
        public Task<string> SaveAndPostInbound(CloudEventOld cloudEvent)
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

        public Task<string> RegisterEvent(CloudEventOld couldEvent)
        {
            // waiting for Benjamins implementatio
            throw new NotImplementedException();
        }
    }
}
