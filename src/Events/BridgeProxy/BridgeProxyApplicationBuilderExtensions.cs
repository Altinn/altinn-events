using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Events.BridgeProxy
{
    /// <summary>
    /// Provides extension methods for registering BridgeProxy endpoints in an ASP.NET Core application.
    /// </summary>
    public static class BridgeProxyApplicationBuilderExtensions
    {
        /// <summary>
        /// Registers the BridgeProxy endpoint route with the specified route prefix.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder to add the route to.</param>
        /// <param name="logger">The logger</param>
        /// <param name="routePrefix">The route prefix to match. Defaults to "/bridge".</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> for chaining.</returns>
        public static IEndpointRouteBuilder MapBridgeProxy(this IEndpointRouteBuilder endpoints, ILogger logger, string routePrefix = "/sblbridge")
        {
            // Matches /bridge and /bridge/anything...
            endpoints.Map("{bridgePrefix=sblbridge}/{**path}", async ctx =>
            {
                // Only allow when first segment equals configured prefix (default "bridge")
                var firstSegment = ctx.Request.Path.Value!.Split('/', 3)[1];
                if (!string.Equals(firstSegment, routePrefix.Trim('/'), System.StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("BridgeProxy: Received request with invalid prefix '{FirstSegment}'. Expected '{RoutePrefix}'. Returning 404.", firstSegment, routePrefix.Trim('/'));
                    ctx.Response.StatusCode = 404;
                    await ctx.Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes($"BridgeProxy: Received request with invalid prefix {firstSegment}. Expected {routePrefix}"));
                    return;
                }

                var handler = ctx.RequestServices.GetRequiredService<BridgeProxyEndpoint>();
                await handler.HandleAsync(ctx);
            });
            return endpoints;
        }
    }
}
