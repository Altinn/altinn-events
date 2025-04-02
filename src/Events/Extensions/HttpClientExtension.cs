#nullable enable

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Altinn.Platform.Events.Extensions
{
    /// <summary>
    /// This extension is created to make it easy to add a bearer token to a HttpRequests. 
    /// </summary>
    public static class HttpClientExtension
    {
        /// <summary>
        /// Extension that add authorization header to request
        /// </summary>
        /// <param name="httpClient">The HttpClient</param>
        /// <param name="authorizationToken">the authorization token (jwt)</param>
        /// <param name="requestUri">The request Uri</param>
        /// <param name="content">The http content</param>
        /// <param name="platformAccessToken">The platformAccess tokens</param>
        /// <returns>A HttpResponseMessage</returns>
        public static Task<HttpResponseMessage> PostAsync(this HttpClient httpClient, string authorizationToken, string requestUri, HttpContent content, string? platformAccessToken = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, new Uri(requestUri, UriKind.Relative));
            request.Headers.Add("Authorization", "Bearer " + authorizationToken);
            request.Content = content;

            if (!string.IsNullOrEmpty(platformAccessToken))
            {
                request.Headers.Add("PlatformAccessToken", platformAccessToken);
            }

            return httpClient.SendAsync(request, CancellationToken.None);
        }

        /// <summary>
        /// Extension that add authorization header to request
        /// </summary>
        /// <param name="httpClient">An instance of the <see cref="HttpClient"/> class.</param>
        /// <param name="requestUri">The URI the request is sent to.</param>
        /// <param name="content">The HTTP request content sent to the server.</param>
        /// <param name="platformAccessToken">
        /// An access token signed by the private platform certificate. Will be included in the request header.
        /// </param> 
        /// <param name="cancellationToken">
        /// A cancellation token that can be used by other objects or threads to receive notice of cancellation.
        /// </param>
        /// <returns>A HttpResponseMessage</returns>
        public static Task<HttpResponseMessage> PostAsync(
            this HttpClient httpClient, 
            string requestUri, 
            HttpContent content, 
            string platformAccessToken, 
            CancellationToken cancellationToken)
        {
            HttpRequestMessage request = new(HttpMethod.Post, new Uri(requestUri, UriKind.Relative))
            {
                Content = content
            };

            request.Headers.Add("PlatformAccessToken", platformAccessToken);

            return httpClient.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Extension that add authorization header to request
        /// </summary>
        /// <param name="httpClient">The HttpClient</param>
        /// <param name="authorizationToken">the authorization token (jwt)</param>
        /// <param name="requestUri">The request Uri</param>
        /// <param name="platformAccessToken">The platformAccess tokens</param>
        /// <returns>A HttpResponseMessage</returns>
        public static Task<HttpResponseMessage> GetAsync(this HttpClient httpClient, string authorizationToken, string requestUri, string? platformAccessToken = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("Authorization", "Bearer " + authorizationToken);
            if (!string.IsNullOrEmpty(platformAccessToken))
            {
                request.Headers.Add("PlatformAccessToken", platformAccessToken);
            }

            return httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
        }
    }
}
