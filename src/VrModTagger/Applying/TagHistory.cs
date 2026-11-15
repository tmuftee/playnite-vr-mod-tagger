using System;
using System.Collections.Generic;
using System.Linq;

namespace VrModTagger.Applying
{
    /// <summary>One game gaining or losing the add-on's tags in one run.</summary>
    public class HistoryEntry
    {
        /// <summary>The run that made the change, so undoing that run can take it out again.</summary>
        public Guid RunId { get; set; }

        /// <summary>When it happened; null for games that were already tagged before history was kept.</summary>
        public DateTime? At { get; set; }

        public Guid GameId { get; set; }
        public string GameName { get; set; }

        /// <summary>False when the game gained tags, true when it lost them.</summary>
        public bool Removed { get; set; }

        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>Sources whose links were added (or removed) in the same change.</summary>
        public List<string> Sources { get; set; } = new List<string>();
    }

    /// <summary>The saved history: every recorded change, newest last.</summary>
    public class TagHistoryData
    {
        /// <summary>Set once games that were tagged before history existed have been recorded.</summary>
        public bool Seeded { get; set; }

        public List<HistoryEntry> Entries { get; set; } = new List<HistoryEntry>();
    }

    /// <summary>Builds and maintains the tagging history. Kept free of Playnite types so it can be tested.</summary>
    public static class TagHistory
    {
        /// <summary>Oldest entries are dropped beyond this, so the file stays small.</summary>
        public const int MaxEntries = 10000;

        /// <summary>The history entries for the changes one run applied.</summary>
        public static List<HistoryEntry> FromChanges(IEnumerable<GameChange> changes, Guid runId, DateTime at, TaggingOptions options)
        {
            var entries = new List<HistoryEntry>();
            foreach (var change in changes)
            {
                var added = change.AddTags.Where(t => TagPlanner.IsOwnedTag(t, options)).ToList();
                if (added.Count > 0)
                {
                    entries.Add(new HistoryEntry
                    {
                        RunId = runId, At = at, GameId = change.GameId, GameName = change.GameName,
                        Tags = added,
                        Sources = SourcesOf(change.AddLinks)
                    });
                }

                var removed = change.RemoveTags.Where(t => TagPlanner.IsOwnedTag(t, options)).ToList();
                if (removed.Count > 0)
                {
                    entries.Add(new HistoryEntry
                    {
                        RunId = runId, At = at, GameId = change.GameId, GameName = change.GameName,
                        Removed = true,
                        Tags = removed,
                        Sources = SourcesOf(change.RemoveLinks)
                    });
                }
            }

            return entries;
        }

        /// <summary>Games that already carry the add-on's tags, recorded with no date when history starts.</summary>
        public static List<HistoryEntry> Seed(IEnumerable<GameState> games, TaggingOptions options)
        {
            return games
                .Select(g => new HistoryEntry
                {
                    RunId = Guid.Empty,
                    At = null,
                    GameId = g.Id,
                    GameName = g.Name,
                    Tags = g.TagNames.Where(t => TagPlanner.IsOwnedTag(t, options)).ToList(),
                    Sources = SourcesOf(g.Links)
                })
                .Where(e => e.Tags.Count > 0)
                .OrderBy(e => e.GameName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>Adds <paramref name="entries"/> and drops the oldest dated entries past <see cref="MaxEntries"/>.</summary>
        public static void Append(TagHistoryData history, IEnumerable<HistoryEntry> entries)
        {
            history.Entries.AddRange(entries);
            var excess = history.Entries.Count - MaxEntries;
            if (excess > 0)
            {
                history.Entries.RemoveRange(0, excess);
            }
        }

        /// <summary>Takes out what an undone run recorded.</summary>
        public static int RemoveRun(TagHistoryData history, Guid runId)
        {
            return runId == Guid.Empty ? 0 : history.Entries.RemoveAll(e => e.RunId == runId);
        }

        /// <summary>Newest first; undated (before history) last, by name.</summary>
        public static List<HistoryEntry> NewestFirst(IEnumerable<HistoryEntry> entries)
        {
            return entries
                .Select((e, i) => new { Entry = e, Index = i })
                .OrderByDescending(x => x.Entry.At.HasValue)
                .ThenByDescending(x => x.Entry.At)
                .ThenByDescending(x => x.Entry.At.HasValue ? x.Index : 0)
                .ThenBy(x => x.Entry.GameName, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => x.Entry)
                .ToList();
        }

        private static List<string> SourcesOf(IEnumerable<LinkInfo> links)
        {
            return links
                .Where(TagPlanner.IsOwnedLink)
                .Select(TagPlanner.LinkSource)
                .Where(s => s.Length > 0)
                .Distinct()
                .ToList();
        }
    }
}
