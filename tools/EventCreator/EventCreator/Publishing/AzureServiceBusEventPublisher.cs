using CloudNative.CloudEvents;

using EventCreator.Clients;
using EventCreator.Configuration;
using EventCreator.Publishing.Wolverine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Wolverine;
using Wolverine.AzureServiceBus;

namespace EventCreator.Publishing;

/// <summary>
/// Publishes to the Azure Service Bus registration queue via a minimal, publish-only Wolverine host,
/// so the message arrives with the Wolverine envelope headers the real registration listener requires.
/// </summary>
public class AzureServiceBusEventPublisher(ServiceBusSettings settings) : IEventPublisher, IAsyncDisposable
{
    private IHost? _host;

    public async Task PublishAsync(CloudEvent cloudEvent)
    {
        IHost host = await GetHost();
        IMessageBus bus = host.Services.GetRequiredService<IMessageBus>();
        string payload = cloudEvent.Serialize();
        await bus.SendAsync(new RegisterEventCommand(payload));
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    private async Task<IHost> GetHost()
    {
        if (_host is null)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();

            // Quiet Wolverine/host info and warning logging
            builder.Logging.SetMinimumLevel(LogLevel.Error);

            builder.Services.AddWolverine(opts =>
            {
                opts.Policies.DisableConventionalLocalRouting();

                // This tool only ever calls IMessageBus.SendAsync, never InvokeAsync, so it has no
                // use for Wolverine's remote-invocation reply routing.
                opts.EnableRemoteInvocation = false;

                // This tool is a single, short-lived publish call, never a clustered node, so
                // Wolverine's own node-coordination queues aren't needed.
                opts.UseAzureServiceBus(settings.ConnectionString).SystemQueuesAreEnabled(false);

                opts.PublishMessage<RegisterEventCommand>()
                    .ToAzureServiceBusQueue(settings.RegistrationQueueName)
                    .SendInline();
            });

            _host = builder.Build();
            await _host.StartAsync();
        }

        return _host;
    }
}
