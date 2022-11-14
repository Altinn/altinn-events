using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

namespace Altinn.Platform.Events.Middleware
{
    /// <summary>
    /// Middleware for buffering request body
    /// </summary>
    public class EnableRequestBodyBufferingMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnableRequestBodyBufferingMiddleware"/> class.
        /// </summary>
        public EnableRequestBodyBufferingMiddleware(RequestDelegate next) =>
            _next = next;

        /// <summary>
        /// Enabled buffering of the request in the provided context.
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            context.Request.EnableBuffering();

            await _next(context);
        }
    }
}
