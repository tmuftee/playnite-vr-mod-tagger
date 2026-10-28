using System;
using System.Collections.Generic;
using System.Linq;
using VrModTagger.Matching;
using VrModTagger.Models;

namespace VrModTagger.Applying
{
    public class TaggingOptions
    {
        public string VrModTagName { get; set; } = "VR Mod";
        public bool IncludeNativeVr { get; set; } = true;
        public string NativeVrTagName { get; set; } = "Native VR";
        public bool AddQuestTag { get; set; }
        public string QuestTagName { get; set; } = "VR Mod: Meta Quest";
        public bool AddPerSourceTags { get; set; }
        public bool RemoveStaleTags { get; set; } = true;
        public HashSet<Guid> ExcludedGameIds { get; set; } = new HashSet<Guid>();

        public string PerSourceTagName(string source) => $"{VrModTagName}: {source}";
    }

    public class LinkInfo
    {
        public string Name { get; set; }
        public string Url { get; set; }

        public override string ToString() => $"{Name} <{Url}>";
    }

    /// <summary>A game's current tags and links, as the planner sees them.</summary>
    public class GameState
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public List<string> TagNames { get; set; } = new List<string>();
        public List<LinkInfo> Links { get; set; } = new List<LinkInfo>();
    }

    /// <summary>What to change on one game. Also the record kept for undo.</summary>
    public class GameChange
    {
        public Guid GameId { get; set; }
        public string GameName { get; set; }
        public List<string> AddTags { get; set; } = new List<string>();
        public List<string> RemoveTags { get; set; } = new List<string>();
        public List<LinkInfo> AddLinks { get; set; } = new List<LinkInfo>();
        public List<LinkInfo> RemoveLinks { get; set; } = new List<LinkInfo>();

        public bool IsEmpty => AddTags.Count == 0 && RemoveTags.Count == 0 && AddLinks.Count == 0 && RemoveLinks.Count == 0;

        public GameChange Inverted() => new GameChange
        {
            GameId = GameId,
            GameName = GameName,
            AddTags = RemoveTags.ToList(),
            RemoveTags = AddTags.ToList(),
            AddLinks = RemoveLinks.ToList(),
            RemoveLinks = AddLinks.ToList()
        };
    }

    /// <summary>
    /// Works out which tags and links to add or remove, without touching Playnite. Only tags and links the
    /// plugin owns are ever removed: the configured tag names, and links named "VR Mod (...)" or "Native VR (...)".
    /// </summary>
    public static class TagPlanner
    {
        public const string VrModLinkPrefix = "VR Mod (";
        public const string NativeVrLinkPrefix = "Native VR (";

        /// <param name="games">Every game in the library.</param>
        /// <param name="matches">Matches from <see cref="GameMatcher"/>. Fuzzy matches are ignored unless approved.</param>
        /// <param name="options">Tag names and switches from the settings.</param>
        /// <param name="failedSources">
        /// Sources that couldn't be fetched this run. Their links are kept, and no tags are removed, because a
        /// missing source would otherwise look like every one of its games losing its VR mod.
        /// </param>
        public static List<GameChange> Plan(
            IEnumerable<GameState> games,
            IEnumerable<GameMatch> matches,
            TaggingOptions options,
            ICollection<string> failedSources = null)
        {
            failedSources = failedSources ?? new List<string>();
            var matchesByGame = matches
                .Where(m => m.IsApplied)
                .GroupBy(m => m.Game.Id)
                .ToDictionary(g => g.Key, g => g.Select(m => m.Entry).Distinct().ToList());

            var changes = new List<GameChange>();
            foreach (var game in games)
            {
                var excluded = options.ExcludedGameIds.Contains(game.Id);
                var entries = !excluded && matchesByGame.TryGetValue(game.Id, out var found) ? found : new List<VrModEntry>();
                if (!options.IncludeNativeVr)
                {
                    entries = entries.Where(e => !IsNativeOnly(e)).ToList();
                }

                var change = PlanGame(game, entries, options, excluded, failedSources);
                if (!change.IsEmpty)
                {
                    changes.Add(change);
                }
            }

            return changes;
        }

        private static GameChange PlanGame(GameState game, List<VrModEntry> entries, TaggingOptions options, bool excluded, ICollection<string> failedSources)
        {
            var change = new GameChange { GameId = game.Id, GameName = game.Name };
            var modEntries = entries.Where(e => !IsNativeOnly(e)).ToList();

            // Tags
            var wantedTags = new List<string>();
            if (modEntries.Count > 0)
            {
                wantedTags.Add(options.VrModTagName);
            }
            if (options.IncludeNativeVr && entries.Any(e => e.NativeVr))
            {
                wantedTags.Add(options.NativeVrTagName);
            }
            // Any standalone Quest version counts, not just games whose only mods are for Quest.
            if (options.AddQuestTag && modEntries.Any(e => e.HasQuestPort || e.QuestOnly))
            {
                wantedTags.Add(options.QuestTagName);
            }
            if (options.AddPerSourceTags)
            {
                wantedTags.AddRange(modEntries.Select(e => options.PerSourceTagName(e.Source)).Distinct());
            }

            var currentTags = new HashSet<string>(game.TagNames, StringComparer.OrdinalIgnoreCase);
            change.AddTags.AddRange(wantedTags.Where(t => !string.IsNullOrWhiteSpace(t) && !currentTags.Contains(t)).Distinct(StringComparer.OrdinalIgnoreCase));

            var canRemoveTags = excluded || (options.RemoveStaleTags && failedSources.Count == 0);
            if (canRemoveTags)
            {
                var wanted = new HashSet<string>(wantedTags, StringComparer.OrdinalIgnoreCase);
                change.RemoveTags.AddRange(game.TagNames.Where(t => IsOwnedTag(t, options) && !wanted.Contains(t)));
            }

            // Links: one per distinct mod page URL.
            // Links come from other sites: anything that isn't an https link to a known source is dropped,
            // and a plugin link already in the library that fails the check is removed below.
            var wantedLinks = entries
                .Where(e => LinkSafety.IsAllowed(e.Url))
                .GroupBy(e => e.Url, StringComparer.OrdinalIgnoreCase)
                .Select(g => new LinkInfo { Name = LinkName(g.First()), Url = g.Key })
                .ToList();
            var wantedUrls = new HashSet<string>(wantedLinks.Select(l => l.Url), StringComparer.OrdinalIgnoreCase);

            foreach (var link in wantedLinks)
            {
                var sameUrl = game.Links.Where(l => string.Equals(l.Url, link.Url, StringComparison.OrdinalIgnoreCase)).ToList();
                if (sameUrl.Any(l => l.Name == link.Name || !IsOwnedLink(l)))
                {
                    continue; // already there, or the user added this URL themselves
                }

                // An owned link with an old name (e.g. it became Quest-only) is renamed.
                change.RemoveLinks.AddRange(sameUrl);
                change.AddLinks.Add(link);
            }

            var canRemoveLinks = excluded || options.RemoveStaleTags;
            change.RemoveLinks.AddRange(game.Links.Where(l =>
                IsOwnedLink(l)
                && !change.RemoveLinks.Contains(l)
                && !wantedUrls.Contains(l.Url ?? "")
                && (
                    // An unsafe link carrying the plugin's name always goes, whatever the settings.
                    !LinkSafety.IsAllowed(l.Url)
                    || (canRemoveLinks && (excluded || !failedSources.Contains(LinkSource(l)))))));

            return change;
        }

        public static string LinkName(VrModEntry entry)
        {
            if (IsNativeOnly(entry))
            {
                return $"{NativeVrLinkPrefix}{entry.Source})";
            }

            return entry.QuestOnly ? $"{VrModLinkPrefix}{entry.Source}, Meta Quest)" : $"{VrModLinkPrefix}{entry.Source})";
        }

        /// <summary>A game with built-in VR and no mod listed.</summary>
        public static bool IsNativeOnly(VrModEntry entry) => entry.NativeVr && entry.ModNames.Count == 0;

        public static bool IsOwnedLink(LinkInfo link)
        {
            return link.Name != null
                && (link.Name.StartsWith(VrModLinkPrefix, StringComparison.Ordinal) || link.Name.StartsWith(NativeVrLinkPrefix, StringComparison.Ordinal))
                && link.Name.EndsWith(")", StringComparison.Ordinal);
        }

        /// <summary>"VR Mod (PCVR Gamer, Meta Quest)" -> "PCVR Gamer".</summary>
        public static string LinkSource(LinkInfo link)
        {
            var start = link.Name.IndexOf('(') + 1;
            var end = link.Name.IndexOfAny(new[] { ',', ')' }, start);
            return end > start ? link.Name.Substring(start, end - start).Trim() : "";
        }

        private static bool IsOwnedTag(string tag, TaggingOptions options)
        {
            return string.Equals(tag, options.VrModTagName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(tag, options.NativeVrTagName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(tag, options.QuestTagName, StringComparison.OrdinalIgnoreCase)
                || tag.StartsWith(options.VrModTagName + ": ", StringComparison.OrdinalIgnoreCase);
        }
    }
}
