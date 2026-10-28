using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VrModTagger.Models;

namespace VrModTagger.Matching
{
    /// <summary>The parts of a Playnite game the matcher needs, so matching can be tested without Playnite.</summary>
    public class LibraryGame
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        /// <summary>Set for games from Steam (the Steam AppID is Playnite's GameId).</summary>
        public uint? SteamAppId { get; set; }

        /// <summary>AppIDs from store.steampowered.com links on the game (often present on GOG/Epic copies).</summary>
        public List<uint> LinkedSteamAppIds { get; set; } = new List<uint>();

        public int? ReleaseYear { get; set; }
    }

    public enum MatchKind
    {
        SteamAppId,
        SteamLink,
        Title,

        /// <summary>Similar but not equal titles. Shown for review instead of being applied.</summary>
        Fuzzy
    }

    public class GameMatch
    {
        public LibraryGame Game { get; set; }
        public VrModEntry Entry { get; set; }
        public MatchKind Kind { get; set; }
        public double Score { get; set; } = 1;

        /// <summary>A fuzzy match the user accepted in the review dialog.</summary>
        public bool Approved { get; set; }

        /// <summary>Whether the match is used for tagging: every exact match, and fuzzy ones once approved.</summary>
        public bool IsApplied => Kind != MatchKind.Fuzzy || Approved;
    }

    /// <summary>
    /// Matches library games to VR mod entries, in order: Steam AppID of a Steam game, Steam AppID from a
    /// store link on the game, normalized title, and (optionally) fuzzy title.
    /// </summary>
    public class GameMatcher
    {
        private readonly Dictionary<uint, List<VrModEntry>> byAppId = new Dictionary<uint, List<VrModEntry>>();
        private readonly Dictionary<string, List<TitleKey>> byTitle = new Dictionary<string, List<TitleKey>>();
        private readonly List<TitleKey> fuzzyCandidates = new List<TitleKey>();
        private readonly bool fuzzy;
        private readonly double fuzzyThreshold;

        // Very short keys ("doom", "prey") are too easy to confuse with other games to match fuzzily.
        private const int MinFuzzyKeyLength = 6;

        private static readonly Regex digitRunRegex = new Regex(@"\d+");

        public GameMatcher(IEnumerable<VrModEntry> entries, bool enableFuzzy = false, double fuzzyThreshold = 0.95)
        {
            fuzzy = enableFuzzy;
            this.fuzzyThreshold = fuzzyThreshold;

            foreach (var entry in entries)
            {
                foreach (var appId in entry.SteamAppIds)
                {
                    Add(byAppId, appId, entry);
                }

                // Games with built-in VR and no mod match only by Steam AppID or their exact name,
                // so "Fallout 4 VR" doesn't tag "Fallout 4" as native VR.
                var nativeOnly = entry.NativeVr && entry.ModNames.Count == 0;
                foreach (var title in new[] { entry.Title }.Concat(entry.AlternateTitles))
                {
                    var keys = new List<NormalizedTitle> { TitleNormalizer.Normalize(title, isModTitle: false) };
                    if (!nativeOnly)
                    {
                        keys.Add(TitleNormalizer.Normalize(title, isModTitle: true));
                    }

                    foreach (var key in keys.Where(k => k.Key.Length > 0))
                    {
                        if (!byTitle.TryGetValue(key.Key, out var list))
                        {
                            byTitle[key.Key] = list = new List<TitleKey>();
                        }

                        if (!list.Any(x => x.Entry == entry))
                        {
                            list.Add(new TitleKey { Key = key.Key, Year = key.Year, Entry = entry });
                        }

                        if (fuzzy && !nativeOnly && key.Key.Length >= MinFuzzyKeyLength)
                        {
                            fuzzyCandidates.Add(new TitleKey { Key = key.Key, Year = key.Year, Entry = entry });
                        }
                    }
                }
            }
        }

        public List<GameMatch> Match(IEnumerable<LibraryGame> games)
        {
            return games.SelectMany(Match).ToList();
        }

        public List<GameMatch> Match(LibraryGame game)
        {
            var matches = new List<GameMatch>();
            void Add(VrModEntry entry, MatchKind kind, double score = 1)
            {
                if (!matches.Any(m => m.Entry == entry))
                {
                    matches.Add(new GameMatch { Game = game, Entry = entry, Kind = kind, Score = score });
                }
            }

            if (game.SteamAppId.HasValue && byAppId.TryGetValue(game.SteamAppId.Value, out var steamEntries))
            {
                steamEntries.ForEach(e => Add(e, MatchKind.SteamAppId));
            }

            foreach (var appId in game.LinkedSteamAppIds)
            {
                if (byAppId.TryGetValue(appId, out var linkedEntries))
                {
                    linkedEntries.ForEach(e => Add(e, MatchKind.SteamLink));
                }
            }

            var gameAppIds = new HashSet<uint>(game.LinkedSteamAppIds);
            if (game.SteamAppId.HasValue)
            {
                gameAppIds.Add(game.SteamAppId.Value);
            }

            var name = TitleNormalizer.Normalize(game.Name, isModTitle: false);
            if (name.Key.Length == 0)
            {
                return matches;
            }

            var gameYear = name.Year ?? game.ReleaseYear;
            if (byTitle.TryGetValue(name.Key, out var titleEntries))
            {
                foreach (var item in titleEntries)
                {
                    if (YearsAgree(item.Year, gameYear) && !SteamIdsDisagree(item.Entry, gameAppIds))
                    {
                        Add(item.Entry, MatchKind.Title);
                    }
                }
            }

            if (fuzzy && matches.Count == 0 && name.Key.Length >= MinFuzzyKeyLength)
            {
                foreach (var candidate in fuzzyCandidates)
                {
                    var score = JaroWinkler(name.Key, candidate.Key);
                    if (score >= fuzzyThreshold
                        && SameNumbers(name.Key, candidate.Key)
                        && YearsAgree(candidate.Year, gameYear)
                        && !SteamIdsDisagree(candidate.Entry, gameAppIds))
                    {
                        var existing = matches.FirstOrDefault(m => m.Entry == candidate.Entry);
                        if (existing == null)
                        {
                            Add(candidate.Entry, MatchKind.Fuzzy, score);
                        }
                        else if (score > existing.Score)
                        {
                            existing.Score = score;
                        }
                    }
                }
            }

            return matches;
        }

        /// <summary>Sequels differ by a number ("titanfall" / "titanfall2"), so similar titles must have the same numbers.</summary>
        private static bool SameNumbers(string a, string b)
        {
            return digitRunRegex.Matches(a).Cast<Match>().Select(m => m.Value)
                .SequenceEqual(digitRunRegex.Matches(b).Cast<Match>().Select(m => m.Value));
        }

        /// <summary>"Prey (2006)" must not match the 2017 Prey; a missing year on either side is fine.</summary>
        private static bool YearsAgree(int? entryYear, int? gameYear)
        {
            return !entryYear.HasValue || !gameYear.HasValue || entryYear == gameYear;
        }

        /// <summary>Both sides know their Steam AppIDs and none are shared: the titles name different games or editions.</summary>
        private static bool SteamIdsDisagree(VrModEntry entry, HashSet<uint> gameAppIds)
        {
            return entry.SteamAppIds.Count > 0 && gameAppIds.Count > 0 && !entry.SteamAppIds.Any(gameAppIds.Contains);
        }

        private static void Add(Dictionary<uint, List<VrModEntry>> index, uint key, VrModEntry entry)
        {
            if (!index.TryGetValue(key, out var list))
            {
                index[key] = list = new List<VrModEntry>();
            }

            if (!list.Contains(entry))
            {
                list.Add(entry);
            }
        }

        private class TitleKey
        {
            public string Key { get; set; }
            public int? Year { get; set; }
            public VrModEntry Entry { get; set; }
        }

        public static double JaroWinkler(string a, string b)
        {
            if (a == b)
            {
                return 1;
            }

            if (a.Length == 0 || b.Length == 0)
            {
                return 0;
            }

            var window = Math.Max(0, Math.Max(a.Length, b.Length) / 2 - 1);
            var aMatched = new bool[a.Length];
            var bMatched = new bool[b.Length];
            var matchCount = 0;
            for (var i = 0; i < a.Length; i++)
            {
                var start = Math.Max(0, i - window);
                var end = Math.Min(b.Length - 1, i + window);
                for (var j = start; j <= end; j++)
                {
                    if (!bMatched[j] && a[i] == b[j])
                    {
                        aMatched[i] = bMatched[j] = true;
                        matchCount++;
                        break;
                    }
                }
            }

            if (matchCount == 0)
            {
                return 0;
            }

            var transpositions = 0;
            for (int i = 0, j = 0; i < a.Length; i++)
            {
                if (!aMatched[i])
                {
                    continue;
                }

                while (!bMatched[j])
                {
                    j++;
                }

                if (a[i] != b[j])
                {
                    transpositions++;
                }

                j++;
            }

            double m = matchCount;
            var jaro = (m / a.Length + m / b.Length + (m - transpositions / 2.0) / m) / 3;
            var prefix = 0;
            while (prefix < Math.Min(4, Math.Min(a.Length, b.Length)) && a[prefix] == b[prefix])
            {
                prefix++;
            }

            return jaro + prefix * 0.1 * (1 - jaro);
        }
    }
}
