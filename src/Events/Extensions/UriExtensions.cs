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
        private static string _urnPattern = @"^urn:[a-z0-9-]{1,30}(:[a-z0-9()+,.\-;$_!\/]{1,100}){1,10}$";

        /// <summary>
        ///  Validates that the provided uri is a urn or an url with the https scheme.
        /// </summary>
        public static bool IsValidUrlOrUrn(Uri uri)
        {
            return uri.Scheme == "https" || IsValidUrn(uri.ToString());
        }

        /// <summary>
        ///  Validates that the provided uri is a urn.
        /// </summary>
        public static bool IsValidUrn(string potentialUrn)
        {
            return Uri.IsWellFormedUriString(potentialUrn, UriKind.Absolute) && Regex.IsMatch(potentialUrn, _urnPattern, RegexOptions.None, TimeSpan.FromSeconds(0.5));
        }
    }
}
