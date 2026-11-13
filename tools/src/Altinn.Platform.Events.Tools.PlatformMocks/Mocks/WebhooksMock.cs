using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Altinn.Platform.Events.Tools.PlatformMocks.Mocks;

/// <summary>
/// A subscriber's webhook. Use <c>https://localhost:{port}/webhooks/{scenario}/{anything}</c> as a subscription's endpoint, so each
/// subscription can have a path of its own. Each request shows up in the mocks' log with its body.
/// <list type="bullet">
/// <item><c>fail</c>: 503 to every request, so the API retries and then dead-letters.</item>
/// <item><c>slow</c>: 200 after a delay.</item>
/// <item><c>unvalidated</c>: 400 to the validation event, so the subscription is never validated, and 200 to the rest.</item>
/// <item>anything else: 200.</item>
/// </list>
/// </summary>
internal sealed class WebhooksMock(int slowSeconds) : IPlatformMock
{
    internal const string ValidationEventType = "platform.events.validatesubscription";

    public string System => "webhooks";

    public void AddMappings(WireMockServer server)
    {
        server
            .Given(Request.Create().WithPath("/webhooks/*").UsingPost())
            .AtPriority(10)
            .RespondWith(Response.Create().WithStatusCode(200));

        server
            .Given(Request.Create().WithPath("/webhooks/fail*").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(503).WithBody("The webhook mock fails on purpose."));

        server
            .Given(Request.Create().WithPath("/webhooks/slow*").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithDelay(TimeSpan.FromSeconds(slowSeconds)));

        server
            .Given(Request.Create().WithPath("/webhooks/unvalidated*").UsingPost().WithBody(body => body?.Contains(ValidationEventType) == true))
            .RespondWith(Response.Create().WithStatusCode(400).WithBody("The webhook mock rejects the validation event on purpose."));

        server
            .Given(Request.Create().WithPath("/webhooks/unvalidated*").UsingPost())
            .AtPriority(5)
            .RespondWith(Response.Create().WithStatusCode(200));
    }
}
