using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.FunctionsOutbound
{
    public class EventsOutbound
    {
        private readonly ILogger<EventsOutbound> _logger;

        public EventsOutbound(ILogger<EventsOutbound> logger)
        {
            _logger = logger;
        }

        [Function(nameof(EventsOutbound))]
        public void Run([QueueTrigger("myqueue-items", Connection = "StorageConnection")] QueueMessage message)
        {
            _logger.LogInformation($"C# Queue trigger function processed: {message.MessageText}");
        }
    }
}
