using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using VrModTagger.Models;

namespace VrModTagger.Sources
{
    /// <summary>
    /// Reads pcvrcentral.com's "VR mods" and "Games with built-in VR" catalogs. Both pages embed their whole
    /// catalog (title, slug, Steam AppID, mods) in the Next.js page data, so two requests cover the site.
    /// </summary>
    public class PcvrCentralSource : IVrModSource
    {
        public const string SourceName = "PCVR Central";
        public const string BaseUrl = "https://pcvrcentral.com";
        public const string ModsUrl = BaseUrl + "/mods";
        public const string NativeVrUrl = BaseUrl + "/native-vr";

        private readonly bool includeNativeVr;

        public string Name => SourceName;
        public string CacheKey => includeNativeVr ? SourceName : SourceName + " (mods only)";

        public PcvrCentralSource(bool includeNativeVr = true)
        {
            this.includeNativeVr = includeNativeVr;
        }

        public async Task<IReadOnlyList<VrModEntry>> FetchAsync(CancellationToken cancellationToken)
        {
            var modsHtml = await SourceHttp.GetStringAsync(ModsUrl, cancellationToken).ConfigureAwait(false);
            var games = PcvrCentralParser.ParseGames(modsHtml);
            if (includeNativeVr)
            {
                var nativeHtml = await SourceHttp.GetStringAsync(NativeVrUrl, cancellationToken).ConfigureAwait(false);
                games.AddRange(PcvrCentralParser.ParseGames(nativeHtml));
            }

            return PcvrCentralParser.ToEntries(games, includeNativeVr);
        }
    }

    public class PcvrCentralGame
    {
        public string Slug { get; set; }
        public string Title { get; set; }
        public uint? SteamAppId { get; set; }

        /// <summary>"none", "supported" (flat game with a VR mode) or "vr-only".</summary>
        public string NativeVr { get; set; }

        public List<PcvrCentralMod> Mods { get; set; } = new List<PcvrCentralMod>();
    }

    public class PcvrCentralMod
    {
        public string Name { get; set; }
        public string Author { get; set; }

        /// <summary>"active", "superseded", "update-detected", "broken" or "abandoned".</summary>
        public string Status { get; set; }

        /// <summary>"pcvr", "quest", "pico".</summary>
        public List<string> Platforms { get; set; } = new List<string>();
    }

    public static class PcvrCentralParser
    {
        // Next.js streams its page data as JS string literals: self.__next_f.push([1,"..."])
        private static readonly Regex flightChunkRegex = new Regex(
            @"self\.__next_f\.push\(\[1,(?<chunk>""(?:[^""\\]|\\.)*"")\]\)",
            RegexOptions.Singleline, SourceHttp.RegexTimeout);

        private const string GamesKey = "\"games\":[";

        public static List<PcvrCentralGame> ParseGames(string html)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
            var payload = new StringBuilder();
            foreach (Match match in flightChunkRegex.Matches(html))
            {
                payload.Append(serializer.Deserialize<string>(match.Groups["chunk"].Value));
            }

            var data = payload.ToString();
            var start = data.IndexOf(GamesKey, StringComparison.Ordinal);
            if (start < 0)
            {
                throw new FormatException("PCVR Central game catalog was not found in the page data; the site layout may have changed.");
            }

            // JavaScriptSerializer treats a "__type" key as a type name. With no type resolver it can't create
            // types, but the key would still make parsing fail, so it's renamed to plain data first.
            var arrayJson = ExtractJsonArray(data, start + GamesKey.Length - 1).Replace("\"__type\"", "\"_type_\"");
            var items = (object[])serializer.DeserializeObject(arrayJson);
            return items.OfType<Dictionary<string, object>>().Select(ToGame).Where(g => g.Slug != null).ToList();
        }

        /// <summary>
        /// One entry per game, with games listed on both pages merged. Mods marked "broken" are left out,
        /// and a game is kept only if it still has a mod or (when <paramref name="includeNativeVr"/>) built-in VR.
        /// </summary>
        public static List<VrModEntry> ToEntries(IEnumerable<PcvrCentralGame> games, bool includeNativeVr = true)
        {
            var entries = new List<VrModEntry>();
            foreach (var group in games.GroupBy(g => g.Slug, StringComparer.OrdinalIgnoreCase))
            {
                var first = group.First();
                var mods = group.SelectMany(g => g.Mods)
                    .Where(m => !string.Equals(m.Status, "broken", StringComparison.OrdinalIgnoreCase))
                    .GroupBy(m => m.Name + "\n" + m.Author)
                    .Select(m => m.First())
                    .ToList();
                var nativeVr = includeNativeVr && group.Any(g => !string.IsNullOrEmpty(g.NativeVr) && g.NativeVr != "none");
                if (mods.Count == 0 && !nativeVr)
                {
                    continue;
                }

                var entry = new VrModEntry
                {
                    Source = PcvrCentralSource.SourceName,
                    Title = first.Title,
                    Url = $"{PcvrCentralSource.BaseUrl}/games/{first.Slug}",
                    NativeVr = nativeVr,
                    ModNames = mods.Select(m => string.IsNullOrEmpty(m.Author) ? m.Name : $"{m.Name} by {m.Author}").ToList(),
                    Platforms = mods.SelectMany(m => m.Platforms).Distinct().ToList()
                };

                var appId = group.Select(g => g.SteamAppId).FirstOrDefault(id => id.HasValue);
                if (appId.HasValue)
                {
                    entry.SteamAppIds.Add(appId.Value);
                }

                // At least one mod runs standalone on Quest (often next to a PC mod, as with Half-Life and Lambda1VR).
                entry.HasQuestPort = entry.Platforms.Contains("quest");

                // Mods exist only for standalone headsets, e.g. Quest/Pico ports.
                entry.QuestOnly = mods.Count > 0 && !nativeVr && !entry.Platforms.Contains("pcvr");
                entries.Add(entry);
            }

            return entries;
        }

        private static PcvrCentralGame ToGame(Dictionary<string, object> item)
        {
            var game = new PcvrCentralGame
            {
                Slug = GetString(item, "slug"),
                Title = GetString(item, "title"),
                NativeVr = GetString(item, "nativeVr"),
                SteamAppId = GetUInt(item, "steamAppId")
            };

            if (item.TryGetValue("mods", out var mods) && mods is IEnumerable modList)
            {
                foreach (var mod in modList.OfType<Dictionary<string, object>>())
                {
                    game.Mods.Add(new PcvrCentralMod
                    {
                        Name = GetString(mod, "name"),
                        Author = GetString(mod, "author"),
                        Status = GetString(mod, "status"),
                        Platforms = mod.TryGetValue("platforms", out var platforms) && platforms is IEnumerable list
                            ? list.OfType<object>().Select(p => DecodeFlightString(p as string)).Where(p => p != null).ToList()
                            : new List<string>()
                    });
                }
            }

            return game;
        }

        private static string GetString(Dictionary<string, object> item, string key)
        {
            return item.TryGetValue(key, out var value) ? DecodeFlightString(value as string) : null;
        }

        private static uint? GetUInt(Dictionary<string, object> item, string key)
        {
            if (!item.TryGetValue(key, out var value) || value == null)
            {
                return null;
            }

            switch (value)
            {
                case int i when i > 0: return (uint)i;
                case long l when l > 0 && l <= uint.MaxValue: return (uint)l;
                case decimal d when d > 0 && d <= uint.MaxValue: return (uint)d;
                case string s when uint.TryParse(s, out var parsed): return parsed;
                default: return null; // "$undefined"
            }
        }

        /// <summary>In Next.js page data "$undefined" means no value and "$$" escapes a literal "$".</summary>
        private static string DecodeFlightString(string value)
        {
            if (value == null || value == "$undefined")
            {
                return null;
            }

            return value.StartsWith("$$", StringComparison.Ordinal) ? value.Substring(1) : value;
        }

        /// <summary>Returns the JSON array that starts at <paramref name="start"/> (a '['), skipping brackets inside strings.</summary>
        private static string ExtractJsonArray(string data, int start)
        {
            var depth = 0;
            var inString = false;
            for (var i = start; i < data.Length; i++)
            {
                var c = data[i];
                if (inString)
                {
                    if (c == '\\')
                    {
                        i++;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                }
                else if (c == '"')
                {
                    inString = true;
                }
                else if (c == '[' || c == '{')
                {
                    depth++;
                }
                else if ((c == ']' || c == '}') && --depth == 0)
                {
                    return data.Substring(start, i - start + 1);
                }
            }

            throw new FormatException("PCVR Central game catalog was cut off.");
        }
    }
}
