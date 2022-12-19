using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Altinn.Platform.Events.Extensions
{
    /// <summary>
    /// Class for extension methods related to hashing a uri
    /// </summary>
    public static class UriExtensions
    {
        /// <summary>
        /// Hashes the provided uri using MD5 algorithm
        /// </summary>
        public static string GetMD5Hash(this Uri uri)
        {
            return GetMD5Hash(uri.ToString());
        }

        /// <summary>
        /// Hashes the provided uri using MD5 algorithm and returns a set
        /// </summary>
        /// <remarks>URLs are split based on '/' for each path segment.
        /// URNs are spli based on ':' for each part in the name specific string. </remarks>
        public static List<string> GetMD5HashSets(this Uri uri)
        {
            List<string> segmentHashes = new List<string>();

            if (uri.Scheme == "https")
            {
                int numSegments = uri.Segments.Length;

                for (int i = 0; i < numSegments; i++)
                {
                    string part = GetUrlUptoNthSegment(uri, i + 1);
                    string hash = GetMD5Hash(part);

                    segmentHashes.Add(hash);
                }
            }
            else if (uri.Scheme == "urn")
            {
                string urn = uri.ToString();
                int numSegments = urn.Count(ch => ch == ':');

                var indexOfDelimiter = urn.IndexOf(':');

                for (int i = 0; i < numSegments; i++)
                {
                    var nextDelimiter = urn.IndexOf(':', indexOfDelimiter + 1);
                    var part = nextDelimiter > 0 ? urn.Substring(0, nextDelimiter) : urn;
                    string hash = GetMD5Hash(part);

                    segmentHashes.Add(hash);

                    indexOfDelimiter = nextDelimiter;
                }
            }

            return segmentHashes;
        }

        private static string GetUrlUptoNthSegment(Uri uri, int n)
        {
            StringBuilder partUri = new(uri.Scheme + Uri.SchemeDelimiter + uri.DnsSafeHost);

            for (int i = 0; i < n; i++)
            {
               partUri.Append(uri.Segments[i]);
            }

            return partUri.ToString().TrimEnd('/');
        }

        private static string GetMD5Hash(string input)
        {
            using MD5 md5 = MD5.Create();

            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            var hash = Convert.ToHexString(hashBytes);
            return hash;
        }
    }
}
