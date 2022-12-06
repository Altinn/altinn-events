using System;
using System.Threading;
using System.Threading.Tasks;

using Azure.Storage.Queues.Models;

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Queues;

namespace Altinn.Platform.Events.Functions.Factories
{
    /// <summary>
    /// Wrapper for queue processor to allow for custom implemtnations
    /// </summary>
    public class QueueProcessorWrapper : QueueProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QueueProcessorWrapper"/> class.
        /// </summary>
        public QueueProcessorWrapper(QueueProcessorOptions options) : base(options)
        {
        }

        /// <inheritdoc/>
        protected override async Task ReleaseMessageAsync(QueueMessage message, FunctionResult result, TimeSpan visibilityTimeout, CancellationToken cancellationToken)
        {
            visibilityTimeout = message.DequeueCount switch
            {
                1 => TimeSpan.FromSeconds(10),
                2 => TimeSpan.FromSeconds(30),
                3 => TimeSpan.FromMinutes(1),
                4 => TimeSpan.FromMinutes(5),
                5 => TimeSpan.FromMinutes(10),
                6 => TimeSpan.FromMinutes(30),
                7 => TimeSpan.FromHours(1),
                8 => TimeSpan.FromHours(3),
                9 => TimeSpan.FromHours(6),
                10 => TimeSpan.FromHours(12),
                11 => TimeSpan.FromHours(12),
                _ => visibilityTimeout,
            };

            await base.ReleaseMessageAsync(message, result, visibilityTimeout, cancellationToken);
        }
    }
}
