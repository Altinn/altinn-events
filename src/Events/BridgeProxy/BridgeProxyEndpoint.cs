using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Altinn.Platform.Events.BridgeProxy
{
    /// <summary>
    /// Handles proxying HTTP requests from the /bridge endpoint to the configured target,
    /// forwarding headers and body as specified in <see cref="BridgeProxyOptions"/>.
    /// </summary>
    internal sealed class BridgeProxyEndpoint(BridgeForwarder forwarder, IOptions<BridgeProxyOptions> options)
    {
        private readonly BridgeForwarder _forwarder = forwarder;
        private readonly BridgeProxyOptions _options = options.Value;

        /// <summary>
        /// Handles the incoming HTTP request for the bridge proxy endpoint.
        /// Proxies the request to the configured target, forwarding headers and body as specified in <see cref="BridgeProxyOptions"/>.
        /// </summary>
        /// <param name="ctx">The current HTTP context for the request.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task HandleAsync(HttpContext ctx)
        {
            // /bridge/{**path} => strip the leading /bridge
            ////var relative = ctx.Request.Path.Value!.Substring("/sblbridge".Length).TrimStart('/');
            ////var targetPathAndQuery = string.IsNullOrEmpty(ctx.Request.QueryString.Value)
            ////    ? relative
            ////    : relative + ctx.Request.QueryString.Value;
            var targetPathAndQuery = ctx.Request.Path.Value + (string.IsNullOrEmpty(ctx.Request.QueryString.Value) ? null : ctx.Request.QueryString.Value);

            var outbound = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), targetPathAndQuery);

            // Headers
            if (_options.ForwardAllHeaders)
            {
                foreach (var h in ctx.Request.Headers)
                {
                    if (h.Key.Equals(HeaderNames.Host, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!outbound.Headers.TryAddWithoutValidation(h.Key, h.Value.ToArray()))
                    {
                        outbound.Content ??= new StreamContent(Stream.Null);
                        outbound.Content.Headers.TryAddWithoutValidation(h.Key, h.Value.ToArray());
                    }
                }
            }
            else
            {
                // Minimal header set
                var allowed = new[] { HeaderNames.Accept, HeaderNames.AcceptEncoding, HeaderNames.UserAgent, HeaderNames.Authorization };
                foreach (var name in allowed)
                {
                    if (ctx.Request.Headers.TryGetValue(name, out var val))
                    {
                        outbound.Headers.TryAddWithoutValidation(name, val.ToArray());
                    }
                }
            }

            // Body
            if (ctx.Request.ContentLength.GetValueOrDefault() > 0)
            {
                outbound.Content = new StreamContent(ctx.Request.Body);
                if (!string.IsNullOrEmpty(ctx.Request.ContentType))
                {
                    outbound.Content.Headers.TryAddWithoutValidation(HeaderNames.ContentType, ctx.Request.ContentType);
                }
            }

            var response = await _forwarder.ForwardAsync(outbound, ctx.RequestAborted).ConfigureAwait(false);

            ctx.Response.StatusCode = (int)response.StatusCode;

            foreach (var header in response.Headers)
            {
                ctx.Response.Headers[header.Key] = header.Value.ToArray();
            }

            if (response.Content != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    ctx.Response.Headers[header.Key] = header.Value.ToArray();
                }

                // Remove transfer-encoding to let ASP.NET Core manage it
                ctx.Response.Headers.Remove(HeaderNames.TransferEncoding);

                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await stream.CopyToAsync(ctx.Response.Body, ctx.RequestAborted).ConfigureAwait(false);
            }
        }
    }
}
