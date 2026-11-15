using System;
using System.Collections.Generic;
using System.Linq;

namespace VrModTagger.Applying
{
    /// <summary>One game in the list shown before changes are applied.</summary>
    public class ChangePreviewRow
    {
        public Guid GameId { get; set; }
        public string GameName { get; set; }
        public string AddedTags { get; set; }
        public string RemovedTags { get; set; }

        /// <summary>Sources whose links are added, e.g. "PCVR Gamer, MrSurvivor".</summary>
        public string AddedLinks { get; set; }

        public string RemovedLinks { get; set; }

        /// <summary>0 gains tags, 1 loses tags, 2 only links change. Used for ordering.</summary>
        public int Group { get; set; }
    }

    public static class ChangePreview
    {
        /// <summary>One row per changed game: games gaining tags first, then losing tags, then link-only changes, each by name.</summary>
        public static List<ChangePreviewRow> Rows(IEnumerable<GameChange> changes)
        {
            return changes
                .Select(c => new ChangePreviewRow
                {
                    GameId = c.GameId,
                    GameName = c.GameName,
                    AddedTags = string.Join(", ", c.AddTags),
                    RemovedTags = string.Join(", ", c.RemoveTags),
                    AddedLinks = Sources(c.AddLinks.Where(a => !c.RemoveLinks.Any(r => SameUrl(r, a)))),
                    RemovedLinks = Sources(c.RemoveLinks.Where(r => !c.AddLinks.Any(a => SameUrl(r, a)))),
                    Group = c.AddTags.Count > 0 ? 0 : c.RemoveTags.Count > 0 ? 1 : 2
                })
                .OrderBy(r => r.Group)
                .ThenBy(r => r.GameName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>"1 game gets" / "3 games get".</summary>
        public static string Games(int count, string singularVerb, string pluralVerb)
        {
            return count == 1 ? $"1 game {singularVerb}" : $"{count} games {pluralVerb}";
        }

        // A renamed link (e.g. it became Quest-only) is a remove plus an add of the same URL; it isn't listed as either.
        private static bool SameUrl(LinkInfo a, LinkInfo b) => string.Equals(a.Url, b.Url, StringComparison.OrdinalIgnoreCase);

        private static string Sources(IEnumerable<LinkInfo> links)
        {
            return string.Join(", ", links.Select(l => TagPlanner.IsOwnedLink(l) ? TagPlanner.LinkSource(l) : l.Name).Distinct());
        }
    }
}
