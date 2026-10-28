using System;
using System.Collections.Generic;
using System.Linq;

namespace VrModTagger.Applying
{
    /// <summary>
    /// Decides which URLs from the sources may be written into the library. Playnite opens links through the
    /// Windows shell, so a link like "file:///C:/x.exe" or a custom protocol would run something on click.
    /// Links come from other people's websites, so only plain https links to the sources' own sites are allowed.
    /// </summary>
    public static class LinkSafety
    {
        private static readonly HashSet<string> allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "pcvrcentral.com",
            "pcvrgamer.net",
            "mrsurvivor-installers.com",
            "github.com"
        };

        public static bool IsAllowed(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url.Any(char.IsControl) || url.Length > 2048)
            {
                return false;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo))
            {
                return false;
            }

            var host = uri.IdnHost.TrimEnd('.');
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                host = host.Substring(4);
            }

            return allowedHosts.Contains(host);
        }
    }
}
