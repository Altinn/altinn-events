using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Altinn.Platform.Events.Extensions
{
    /// <summary>
    /// This extension is created to make it easy to add a bearer token to a HttpRequests. 
    /// </summary>
    public static class HttpRequestExtension
    {
        /// <summary>
        /// Retrieves raw string using specified encoding, or UTF8 otherwise.
        /// </summary>
        /// <remarks>Remember to add to service configuration.</remarks>
        /// <param name="request">Request input</param>
        /// <param name="encoding">Optional encoding</param>
        /// <returns></returns>
        public static async Task<string> GetRawBodyAsync(this HttpRequest request, Encoding encoding = null)
        {
            if (!request.Body.CanSeek)
            {
                // We only do this if the stream isn't *already* seekable,
                // as EnableBuffering will create a new stream instance
                // each time it's called
                request.EnableBuffering();
            }

            request.Body.Position = 0;

            var reader = new StreamReader(request.Body, encoding ?? Encoding.UTF8);

            var body = await reader.ReadToEndAsync().ConfigureAwait(false);

            request.Body.Position = 0;

            return body;
        }
    }
}
