using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VrModTagger.Matching;
using VrModTagger.Models;

namespace VrModTagger.Sources
{
    /// <summary>
    /// Reads the "PCVR MODS" posts from pcvrgamer.net through the WordPress REST API.
    /// </summary>
    public class PcvrGamerSource : IVrModSource
    {
        public const string SourceName = "PCVR Gamer";
        public const int ModsCategoryId = 26;
        private const int PageSize = 100;
        private const string PostsUrl = "https://pcvrgamer.net/wp-json/wp/v2/posts?categories={0}&per_page={1}&page={2}&_fields=id,slug,link,title,content,categories";

        public string Name => SourceName;
        public string CacheKey => SourceName;

        public async Task<IReadOnlyList<VrModEntry>> FetchAsync(CancellationToken cancellationToken)
        {
            var entries = new List<VrModEntry>();
            for (var page = 1; ; page++)
            {
                var url = string.Format(PostsUrl, ModsCategoryId, PageSize, page);
                var json = await SourceHttp.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
                var posts = PcvrGamerParser.ParsePosts(json);
                entries.AddRange(posts.Select(PcvrGamerParser.ToEntry));

                // A short page is the last one; asking past the end returns HTTP 400.
                if (posts.Count < PageSize)
                {
                    break;
                }
            }

            return entries;
        }
    }

    public static class PcvrGamerParser
    {
        private static readonly Regex anchorRegex = new Regex(
            @"<a\s[^>]*href=""(?<href>[^""]*)""[^>]*>(?<text>.*?)</a>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex steamAppRegex = new Regex(
            @"store\.steampowered\.com/(?:agecheck/)?app/(?<id>\d+)(?:/(?<slug>[^/?#""]+))?",
            RegexOptions.IgnoreCase);

        private static readonly Regex tagRegex = new Regex(@"<[^>]+>", RegexOptions.Singleline);

        // A sentence ends at . ! ? followed by a capital letter, so abbreviations like "R.D.R. on Steam" stay whole.
        private static readonly Regex sentenceBreakRegex = new Regex(@"(?<=[.!?])\s+(?=[A-Z])");

        private static readonly Regex buyNameRegex = new Regex(@"\bbuy\s+(?<name>.+?)\s+on\s+steam\b", RegexOptions.IgnoreCase);

        // Anchor and "Buy ... on Steam" texts that don't name a game.
        private static readonly HashSet<string> genericAnchorTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "here", "get the mod here", "steam", "link", "the game", "this game", "buy", "buy here", "buy the game", "steam page", "store"
        };

        public static List<WpPost> ParsePosts(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var posts = (List<WpPost>)new DataContractJsonSerializer(typeof(List<WpPost>)).ReadObject(stream);
                return posts ?? new List<WpPost>();
            }
        }

        public static VrModEntry ToEntry(WpPost post)
        {
            var entry = new VrModEntry
            {
                Source = PcvrGamerSource.SourceName,
                Title = CleanText(post.Title?.Rendered ?? post.Slug ?? ""),
                Url = post.Link
            };

            foreach (var link in FindGameSteamLinks(entry.Title, post.Content?.Rendered ?? ""))
            {
                if (!entry.SteamAppIds.Contains(link.AppId))
                {
                    entry.SteamAppIds.Add(link.AppId);
                }

                if (link.GameName != null
                    && !string.Equals(link.GameName, entry.Title, StringComparison.OrdinalIgnoreCase)
                    && !entry.AlternateTitles.Contains(link.GameName, StringComparer.OrdinalIgnoreCase))
                {
                    entry.AlternateTitles.Add(link.GameName);
                }
            }

            return entry;
        }

        public class SteamLink
        {
            public uint AppId { get; set; }

            /// <summary>Game name from the anchor text, when the anchor names a game rather than saying "HERE".</summary>
            public string GameName { get; set; }
        }

        /// <summary>
        /// Steam links in a post that point at the game the mod is for. Posts also link the modder's own
        /// games ("Support sqofer's work by buying his VR game on Steam HERE"), which must not be used.
        /// A link counts when its store slug or anchor text matches the post title, or when the text
        /// before it says to buy the game ("Buy Dead Space 2 on Steam HERE"). Named links that follow straight
        /// on from a buy link (a list of supported games) count too.
        /// </summary>
        public static List<SteamLink> FindGameSteamLinks(string postTitle, string contentHtml)
        {
            var links = new List<SteamLink>();
            var titleKey = RoughKey(postTitle);
            var previousEnd = 0;
            var previousWasBuyLink = false;

            foreach (Match anchor in anchorRegex.Matches(contentHtml))
            {
                var context = CleanText(contentHtml.Substring(previousEnd, anchor.Index - previousEnd));
                previousEnd = anchor.Index + anchor.Length;

                var anchorText = CleanText(anchor.Groups["text"].Value);
                var gameName = genericAnchorTexts.Contains(anchorText) || anchorText.Length == 0 ? null : anchorText;

                // Tracked for every link, Steam or not: "You can buy these games here: Duke Nukem 3D (zoom-platform), BLOOD (Steam), ..."
                var isBuyLink = IsBuyContext(context) || (previousWasBuyLink && gameName != null && IsListSeparator(context));
                previousWasBuyLink = isBuyLink;

                var steam = steamAppRegex.Match(anchor.Groups["href"].Value);
                if (!steam.Success || !uint.TryParse(steam.Groups["id"].Value, out var appId))
                {
                    continue;
                }

                var slugName = steam.Groups["slug"].Success ? WebUtility.UrlDecode(steam.Groups["slug"].Value).Replace('_', ' ').Trim() : null;
                if (isBuyLink || RoughMatch(titleKey, slugName) || RoughMatch(titleKey, gameName))
                {
                    // Post titles are often abbreviated ("R.D.R."), so the full name is worth keeping:
                    // anchor text, then "Buy Red Dead Redemption on Steam", then the store URL slug.
                    var buyName = buyNameRegex.Matches(context).Cast<Match>().LastOrDefault()?.Groups["name"].Value.Trim();
                    if (buyName != null && genericAnchorTexts.Contains(buyName))
                    {
                        buyName = null; // "BUY THE GAME ON STEAM HERE"
                    }
                    links.Add(new SteamLink { AppId = appId, GameName = gameName ?? NullIfEmpty(buyName) ?? NullIfEmpty(slugName) });
                }
            }

            return links;
        }

        private static bool IsBuyContext(string context)
        {
            // Only the sentence leading into the link: "This mod includes VR support for 6 base games. You can buy these games here:"
            var text = sentenceBreakRegex.Split(context).Last().ToLowerInvariant();
            if (text.Contains("support") || text.Contains("donat"))
            {
                return false;
            }

            return text.Contains("buy") || text.Contains("on steam");
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static bool IsListSeparator(string context)
        {
            return context.All(c => char.IsWhiteSpace(c) || char.IsPunctuation(c));
        }

        private static bool RoughMatch(string titleKey, string other)
        {
            if (string.IsNullOrEmpty(titleKey) || string.IsNullOrEmpty(other))
            {
                return false;
            }

            var otherKey = RoughKey(other);
            return otherKey.Length > 0 && (otherKey.Contains(titleKey) || titleKey.Contains(otherKey));
        }

        private static string RoughKey(string title)
        {
            return TitleNormalizer.Normalize(title, isModTitle: false).Key;
        }

        private static string CleanText(string html)
        {
            var text = WebUtility.HtmlDecode(tagRegex.Replace(html, " "));
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        [DataContract]
        public class WpPost
        {
            [DataMember(Name = "id")] public long Id { get; set; }
            [DataMember(Name = "slug")] public string Slug { get; set; }
            [DataMember(Name = "link")] public string Link { get; set; }
            [DataMember(Name = "title")] public WpRendered Title { get; set; }
            [DataMember(Name = "content")] public WpRendered Content { get; set; }
            [DataMember(Name = "categories")] public List<int> Categories { get; set; }
        }

        [DataContract]
        public class WpRendered
        {
            [DataMember(Name = "rendered")] public string Rendered { get; set; }
        }
    }
}
