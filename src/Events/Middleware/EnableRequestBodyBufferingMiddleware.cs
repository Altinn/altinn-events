using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Altinn.Platform.Events.Middleware
{
    /// <summary>
    /// Enable buffering when using raw request body extraction
    /// </summary>
    public class EnableRequestBodyBufferingMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Set delegate
        /// </summary>
        /// <param name="next">Next delegate</param>
        public EnableRequestBodyBufferingMiddleware(RequestDelegate next) =>
            _next = next;

        /// <summary>
        /// InvokeAsync
        /// </summary>
        /// <param name="context">HttpContext</param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context)
        {
            context.Request.EnableBuffering();

            await _next(context);
        }
    }
}
