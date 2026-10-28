using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VrModTagger.Matching
{
    /// <summary>A title reduced to a comparison key, with any "(2016)"-style year kept apart.</summary>
    public sealed class NormalizedTitle
    {
        public string Key { get; set; }
        public int? Year { get; set; }

        public override string ToString() => Year.HasValue ? $"{Key} ({Year})" : Key;
    }

    /// <summary>
    /// Turns titles into keys that compare equal across sources and Playnite:
    /// "Alien: Isolation" and "Alien Isolation" -> "alienisolation", "Half-Life 2: Episode Two" -> "halflife2episode2".
    /// </summary>
    public static class TitleNormalizer
    {
        private static readonly Regex yearRegex = new Regex(@"\((?<year>(?:19|20)\d{2})\)");
        private static readonly Regex bracketRegex = new Regex(@"\([^)]*\)|\[[^\]]*\]");

        // Mod titles only: "DOOM (2016) VR — KHARVOX". (" by Modder" isn't stripped: it can't be told apart from "Bound by Flame".)
        private static readonly Regex dashSuffixRegex = new Regex(@"\s+[—–]\s+.*$");

        private static readonly Regex nonAlphanumericRegex = new Regex(@"[^a-z0-9]+");

        // "x" is left alone: it is far more often a letter ("X-Wing", "Mega Man X", "XCOM") than 10.
        private static readonly Dictionary<string, string> romanNumerals = new Dictionary<string, string>
        {
            ["i"] = "1", ["ii"] = "2", ["iii"] = "3", ["iv"] = "4", ["v"] = "5",
            ["vi"] = "6", ["vii"] = "7", ["viii"] = "8", ["ix"] = "9",
            ["xi"] = "11", ["xii"] = "12", ["xiii"] = "13", ["xiv"] = "14", ["xv"] = "15",
            ["xvi"] = "16", ["xvii"] = "17", ["xviii"] = "18", ["xix"] = "19", ["xx"] = "20"
        };

        private static readonly Dictionary<string, string[]> abbreviations = new Dictionary<string, string[]>
        {
            ["gta"] = new[] { "grand", "theft", "auto" }
        };

        // Only expanded as the last word, where they stand for an edition.
        private static readonly Dictionary<string, string[]> finalAbbreviations = new Dictionary<string, string[]>
        {
            ["de"] = new[] { "definitive", "edition" },
            ["le"] = new[] { "legendary", "edition" },
            ["se"] = new[] { "special", "edition" }
        };

        // Removed from the end of every title, so editions of one game share a key.
        // "Remastered" and "Remake" are kept: those are usually separate games with separate mods.
        private static readonly string[][] editionSuffixes = new[]
        {
            "game of the year enhanced", "game of the year edition", "game of the year", "goty enhanced", "goty edition", "goty",
            "definitive edition", "enhanced edition", "complete edition", "deluxe edition",
            "ultimate edition", "gold edition", "special edition", "anniversary edition",
            "standard edition", "directors cut"
        }.Select(s => s.Split(' ')).ToArray();

        // Removed from the end of mod titles only: "Dishonored VR", "Half-Life 2 Quest".
        private static readonly HashSet<string> modSuffixWords = new HashSet<string> { "vr", "pcvr", "xr", "quest", "vro" };

        /// <summary>
        /// Every key a mod title should be found under: as written, without mod words, and without a trailing "1".
        /// Mod sites write "Half Life 1" or "Quake 1" for the first game, which stores call "Half-Life" and "Quake",
        /// while "Battlefield 1 VR" must still find "Battlefield 1".
        /// </summary>
        public static List<NormalizedTitle> ModTitleKeys(string title)
        {
            return new[]
                {
                    Normalize(title, isModTitle: false),
                    Normalize(title, isModTitle: true),
                    Normalize(title, isModTitle: true, dropFirstGameNumber: true)
                }
                .Where(k => k.Key.Length > 0)
                .GroupBy(k => k.Key)
                .Select(g => g.First())
                .ToList();
        }

        /// <param name="title">The title to normalize.</param>
        /// <param name="isModTitle">
        /// True for titles from VR mod sources, which carry extra words like "VR", "(motion controls)" or "— ModName".
        /// False for Playnite game names and native VR games, where "VR" is part of the real name ("Fallout 4 VR").
        /// </param>
        /// <param name="dropFirstGameNumber">Mod titles only: also drop a trailing "1" ("Half Life 1" -> "halflife").</param>
        public static NormalizedTitle Normalize(string title, bool isModTitle, bool dropFirstGameNumber = false)
        {
            var result = new NormalizedTitle { Key = "" };
            if (string.IsNullOrWhiteSpace(title))
            {
                return result;
            }

            var text = RemoveDiacritics(title).ToLowerInvariant()
                .Replace('’', '\'').Replace('‘', '\'').Replace('`', '\'').Replace('´', '\'')
                .Replace("™", "").Replace("®", "").Replace("©", "");

            var year = yearRegex.Match(text);
            if (year.Success)
            {
                result.Year = int.Parse(year.Groups["year"].Value, CultureInfo.InvariantCulture);
            }

            if (isModTitle)
            {
                text = dashSuffixRegex.Replace(text, "");
            }

            text = bracketRegex.Replace(text, " ");
            text = text.Replace("&", " and ");

            // "R.E.P.O." -> "repo", "Assassin's" -> "assassins"
            text = text.Replace(".", "").Replace("'", "");

            var tokens = nonAlphanumericRegex.Split(text)
                .Where(t => t.Length > 0)
                .Select(t => romanNumerals.TryGetValue(t, out var digits) ? digits : t)
                .SelectMany(t => abbreviations.TryGetValue(t, out var words) ? words : new[] { t })
                .ToList();

            ApplyEndingRules(tokens);
            if (isModTitle)
            {
                var withoutModWords = tokens.ToList();
                while (withoutModWords.Count > 0)
                {
                    var last = withoutModWords[withoutModWords.Count - 1];
                    // "Alba VR Mod" loses "Mod", but "Garry's Mod VR" and "The Dark Mod VR" keep theirs.
                    var isVrMod = last == "mod" && withoutModWords.Count > 1 && modSuffixWords.Contains(withoutModWords[withoutModWords.Count - 2]);
                    if (!isVrMod && !modSuffixWords.Contains(last))
                    {
                        break;
                    }

                    withoutModWords.RemoveAt(withoutModWords.Count - 1);
                }

                if (dropFirstGameNumber && withoutModWords.Count > 1 && withoutModWords[withoutModWords.Count - 1] == "1")
                {
                    withoutModWords.RemoveAt(withoutModWords.Count - 1);
                }

                // A title that is only "VR" keeps its words.
                if (withoutModWords.Count > 0)
                {
                    tokens = withoutModWords;
                    ApplyEndingRules(tokens); // "Star Wars - X-Wing SE VR"
                }
            }

            result.Key = string.Concat(tokens);
            return result;
        }

        private static void ApplyEndingRules(List<string> tokens)
        {
            // "GTA: San Andreas DE", "Mass Effect: LE", "Star Wars - X-Wing SE"
            if (tokens.Count > 1 && finalAbbreviations.TryGetValue(tokens[tokens.Count - 1], out var expanded))
            {
                tokens.RemoveAt(tokens.Count - 1);
                tokens.AddRange(expanded);
            }

            RemoveEditionSuffixes(tokens);
        }

        private static void RemoveEditionSuffixes(List<string> tokens)
        {
            bool removed;
            do
            {
                removed = false;
                foreach (var suffix in editionSuffixes)
                {
                    if (tokens.Count > suffix.Length && EndsWith(tokens, suffix))
                    {
                        tokens.RemoveRange(tokens.Count - suffix.Length, suffix.Length);

                        // "GTA 4: The Complete Edition"
                        if (tokens.Count > 1 && tokens[tokens.Count - 1] == "the")
                        {
                            tokens.RemoveAt(tokens.Count - 1);
                        }

                        removed = true;
                        break;
                    }
                }
            }
            while (removed);
        }

        private static bool EndsWith(List<string> tokens, string[] suffix)
        {
            var offset = tokens.Count - suffix.Length;
            for (var i = 0; i < suffix.Length; i++)
            {
                if (tokens[offset + i] != suffix[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static string RemoveDiacritics(string text)
        {
            var decomposed = text.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
