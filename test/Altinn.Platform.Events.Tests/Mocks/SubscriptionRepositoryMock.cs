using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Events.Models;
using Altinn.Platform.Events.Repository;
using Altinn.Platform.Events.Tests.Models;

using Newtonsoft.Json;

namespace Altinn.Platform.Events.Tests.Mocks
{
    /// <summary>
    /// Represents a mock implementation of <see cref="ISubscriptionRepository"/>.
    /// </summary>
    public class SubscriptionRepositoryMock : ISubscriptionRepository
    {
        /// <summary>
        /// Returns null regardless of input.
        /// </summary>
        public Task<Subscription> FindSubscription(Subscription eventsSubscription, CancellationToken ct)
        {
            return Task.FromResult((Subscription)null);
        }

        public Task<Subscription> CreateSubscription(Subscription eventsSubscription, string sourceFilterHash)
        {
            Random rnd = new();
            eventsSubscription.Id = rnd.Next(1, int.MaxValue);
            eventsSubscription.Created = DateTime.Now;
            return Task.FromResult(eventsSubscription);
        }

        public Task DeleteSubscription(int id)
        {
            return Task.CompletedTask;
        }

        public Task<Subscription> GetSubscription(int id)
        {
            return Task.FromResult(new Subscription() { Id = id, AlternativeSubjectFilter = "/organisation/950474084", CreatedBy = "/organisation/950474084" });
        }

        public async Task<List<Subscription>> GetSubscriptionsByConsumer(string consumer, bool includeInvalid)
        {
            await Task.CompletedTask;

            string subscriptionsPath = Path.Combine(GetSubscriptionPath(), "1.json");
            if (File.Exists(subscriptionsPath))
            {
                string content = File.ReadAllText(subscriptionsPath);
                List<Subscription> allSubscriptions = JsonConvert.DeserializeObject<List<Subscription>>(content);

                if (consumer.EndsWith('%'))
                {
                    consumer = consumer.Replace("%", string.Empty);
                    return allSubscriptions.Where(s => s.Consumer.StartsWith(consumer)).ToList();
                }

                return allSubscriptions.Where(s => s.Consumer == consumer).ToList();
            }
            else
            {
                return new List<Subscription>();
            }
        }

        public Task SetValidSubscription(int id)
        {
            return Task.CompletedTask;
        }

        public Task<List<Subscription>> GetSubscriptions(string resource, string subject, string type, CancellationToken ct)
        {
            List<Subscription> subscriptions = new();

            string subscriptionsPath = Path.Combine(GetSubscriptionPath(), "1.json");
            List<SubscriptionTableEntry> subscriptionEntries = null;
            if (File.Exists(subscriptionsPath))
            {
                string content = File.ReadAllText(subscriptionsPath);
                subscriptionEntries = JsonConvert.DeserializeObject<List<SubscriptionTableEntry>>(content);
            }
            else
            {
                subscriptionEntries = new List<SubscriptionTableEntry>();
            }

            subscriptions = subscriptionEntries
                .Where(s => s.ResourceFilter == resource
                    && (string.IsNullOrEmpty(s.TypeFilter) || s.TypeFilter.Equals(type))
                    && (string.IsNullOrEmpty(s.SubjectFilter) || s.SubjectFilter.Equals(subject)))
                .Select(s =>
                    new Subscription
                    {
                        Id = s.Id,
                        SourceFilter = s.SourceFilter,
                        AlternativeSubjectFilter = s.AlternativeSubjectFilter,
                        Consumer = s.Consumer,
                        EndPoint = s.EndPoint
                    })
                .ToList();

            return Task.FromResult(subscriptions);
        }

        private static string GetSubscriptionPath()
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(EventsServiceMock).Assembly.Location).LocalPath);
            return Path.Combine(unitTestFolder, "..", "..", "..", "Data", "subscriptions");
        }
    }
}
