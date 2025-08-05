using System;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.FunctionsOutbound
{
    public class SubscriptionValidation
    {
        private readonly ILogger<SubscriptionValidation> _logger;

        public SubscriptionValidation(ILogger<SubscriptionValidation> logger)
        {
            _logger = logger;
        }

        [Function(nameof(SubscriptionValidation))]
        public void Run([QueueTrigger("myqueue-items", Connection = "")] QueueMessage message)
        {
            _logger.LogInformation($"C# Queue trigger function processed: {message.MessageText}");
        }
    }
}
