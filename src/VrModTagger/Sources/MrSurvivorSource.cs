using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VrModTagger.Models;

namespace VrModTagger.Sources
{
    /// <summary>
    /// Reads the "VR HUB Game List" table on mrsurvivor-installers.com.
    /// </summary>
    public class MrSurvivorSource : IVrModSource
    {
        public const string SourceName = "MrSurvivor";
        public const string PageUrl = "https://mrsurvivor-installers.com/";
        public const string LinkUrl = "https://mrsurvivor-installers.com/#vr-hub";

        public string Name => SourceName;
        public string CacheKey => SourceName;

        public async Task<IReadOnlyList<VrModEntry>> FetchAsync(CancellationToken cancellationToken)
        {
            var html = await SourceHttp.GetStringAsync(PageUrl, cancellationToken).ConfigureAwait(false);
            return MrSurvivorParser.Parse(html);
        }
    }

    public static class MrSurvivorParser
    {
        private static readonly Regex tableRegex = new Regex(
            @"<table[^>]*class=""[^""]*game-list-table[^""]*""[^>]*>(?<body>.*?)</table>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex rowRegex = new Regex(
            @"<tr[^>]*>\s*<td[^>]*>(?<title>.*?)</td>\s*<td[^>]*>(?<platforms>.*?)</td>\s*</tr>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex tagRegex = new Regex(@"<[^>]+>", RegexOptions.Singleline);

        // Platform words that mean the mod runs on PC, so the row isn't Quest-only.
        private static readonly string[] pcPlatformWords =
        {
            "steam", "gog", "epic", "pcvr", "windows", "battle.net", "manual", "retail", "microsoft store", "xbox", "ea app", "ubisoft", "openxr"
        };

        private const string Dash = " — ";

        public static List<VrModEntry> Parse(string html)
        {
            var table = tableRegex.Match(html);
            if (!table.Success)
            {
                throw new FormatException("MrSurvivor game list table was not found; the page layout may have changed.");
            }

            var entries = new List<VrModEntry>();
            foreach (Match row in rowRegex.Matches(table.Groups["body"].Value))
            {
                var title = CleanText(row.Groups["title"].Value);
                var platformText = CleanText(row.Groups["platforms"].Value);
                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }

                var entry = new VrModEntry
                {
                    Source = MrSurvivorSource.SourceName,
                    Title = title,
                    Url = MrSurvivorSource.LinkUrl
                };

                // The part after the dash is usually the mod's name ("— KHARVOX", "— Overlord") but sometimes
                // the game's ("Galaxy Quest — Super Mario Galaxy"). One word is treated as a mod name, since a
                // game called "Overlord" exists; longer text is also kept as a title. Platform notes are skipped.
                var dash = title.IndexOf(Dash, StringComparison.Ordinal);
                if (dash > 0)
                {
                    var suffix = title.Substring(dash + Dash.Length).Trim();
                    if (!IsPcPlatform(suffix) && suffix.IndexOf("quest", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        entry.ModNames.Add(suffix);
                        if (suffix.Split(' ').Length >= 2)
                        {
                            entry.AlternateTitles.Add(suffix);
                        }
                    }
                }

                foreach (var part in platformText.Split('|').Select(p => p.Trim()).Where(p => p.Length > 0))
                {
                    var platform = part;
                    var platformDash = platform.IndexOf(Dash, StringComparison.Ordinal);
                    if (platformDash > 0)
                    {
                        var note = platform.Substring(platformDash + Dash.Length).Trim();
                        platform = platform.Substring(0, platformDash).Trim();
                        if (note.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            entry.TemporarilyUnavailable = true;
                        }
                        else
                        {
                            // "Steam — Halo: The Master Chief Collection" names the actual game.
                            entry.AlternateTitles.Add(note);
                        }
                    }

                    entry.Platforms.Add(platform);
                }

                entry.QuestOnly = entry.Platforms.Any(p => p.IndexOf("quest", StringComparison.OrdinalIgnoreCase) >= 0)
                    && !entry.Platforms.Any(IsPcPlatform);
                entries.Add(entry);
            }

            if (entries.Count == 0)
            {
                throw new FormatException("MrSurvivor game list table had no rows; the page layout may have changed.");
            }

            return entries;
        }

        private static bool IsPcPlatform(string platform)
        {
            return pcPlatformWords.Any(word => platform.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string CleanText(string html)
        {
            var text = WebUtility.HtmlDecode(tagRegex.Replace(html, " "));
            return Regex.Replace(text, @"\s+", " ").Trim();
        }
    }
}
