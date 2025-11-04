using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Altinn.Platform.Events.BridgeProxy
{
    /// <summary>
    /// Provides functionality to forward HTTP requests using an <see cref="HttpClient"/>.
    /// </summary>
    internal sealed class BridgeForwarder
    {
        private readonly HttpClient _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="BridgeForwarder"/> class.
        /// </summary>
        /// <param name="client">The <see cref="HttpClient"/> used to send requests.</param>
        public BridgeForwarder(HttpClient client) => _client = client;

        /// <summary>
        /// Forwards the specified HTTP request asynchronously.
        /// </summary>
        /// <param name="outbound">The outbound <see cref="HttpRequestMessage"/> to forward.</param>
        /// <param name="ct">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task{HttpResponseMessage}"/> representing the asynchronous operation.</returns>
        public Task<HttpResponseMessage> ForwardAsync(HttpRequestMessage outbound, CancellationToken ct) =>
            _client.SendAsync(outbound, HttpCompletionOption.ResponseHeadersRead, ct);
    }
}
