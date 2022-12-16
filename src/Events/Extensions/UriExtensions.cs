using System;
using System.Collections.Generic;
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
        public static List<string> GetMD5HashSets(this Uri uri)
        {
            if (uri.Scheme == "https" || uri.Scheme == "urn")
            {
                int numSegments = uri.Segments.Length;
                List<string> segmentHashes = new List<string>();

                for (int i = 0; i < numSegments; i++)
                {
                    string part = GetUriUptoNthSegment(uri, i + 1);
                    string hash = GetMD5Hash(part);

                    Console.WriteLine("[" + i + "]: " + hash + " - " + part);
                    segmentHashes.Add(hash);
                }

                return segmentHashes;
            }

            return null;
        }

        private static string GetUriUptoNthSegment(Uri uri, int n)
        {
            string partUri = uri.Scheme + Uri.SchemeDelimiter + uri.DnsSafeHost;

            for (int i = 0; i < n; i++)
            {
                partUri += uri.Segments[i];
            }

            return partUri.TrimEnd('/');
        }

        private static string GetMD5Hash(string input)
        {
            using MD5 md5 = MD5.Create();

            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            var hash = Convert.ToHexString(hashBytes);
            Console.WriteLine($"Hash {input} \t as \t {hash}");
            return hash;
        }
    }
}
