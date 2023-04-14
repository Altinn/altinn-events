using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Altinn.Platform.Events.Extensions
{
    /// <summary>
    /// Class for extension methods related to hashing a uri
    /// </summary>
    public static class UriExtensions
    {
        private static string _urnPattern = @"^urn:[a-z][a-z0-9-]{0,31}(:[a-z][a-z0-9()+,.\-=@;$_!*'%#]{0,99}){1,10}\/?$";

        /// <summary>
        /// Hashes the provided uri using MD5 algorithm
        /// </summary>
        public static string GetMD5Hash(this Uri uri)
        {
            return GetMD5Hash(uri.ToString().TrimEnd('/'));
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

        /// <summary>
        ///  Validates that the provided uri is a urn or an url with the https scheme.
        /// </summary>
        public static bool IsValidUrlOrUrn(Uri uri)
        {
            return uri.Scheme == "https" || Regex.IsMatch(uri.ToString(), _urnPattern, RegexOptions.None, TimeSpan.FromSeconds(0.5));
        }

        /// <summary>
        ///  Validates that the provided uri is a urn.
        /// </summary>
        public static bool IsValidUrn(string potentialUrn)
        {
            return Regex.IsMatch(potentialUrn, _urnPattern, RegexOptions.None, TimeSpan.FromSeconds(0.5));
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
