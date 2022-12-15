using System;
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
        public static string MD5HashUri(this Uri uri)
        {
            string input = uri.ToString();
            using MD5 md5 = MD5.Create();

            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            return Convert.ToHexString(hashBytes);      
        }
    }
}
