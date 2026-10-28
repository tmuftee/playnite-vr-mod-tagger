using System;
using System.Collections.Generic;
using System.Linq;
using VrModTagger.Models;

namespace VrModTagger.Matching
{
    /// <summary>The user's answer for one suggested (fuzzy) match, kept so it isn't asked again.</summary>
    public class FuzzyDecision
    {
        public Guid GameId { get; set; }
        public string EntryKey { get; set; }
        public bool Approved { get; set; }
    }

    public static class FuzzyReview
    {
        /// <summary>Identifies an entry across runs. URL alone isn't enough: every MrSurvivor entry shares one.</summary>
        public static string EntryKey(VrModEntry entry) => $"{entry.Source}|{entry.Url}|{entry.Title}";

        /// <summary>Marks fuzzy matches the user approved before and returns those not yet decided.</summary>
        public static List<GameMatch> ApplyDecisions(IEnumerable<GameMatch> matches, IEnumerable<FuzzyDecision> decisions)
        {
            // No ValueTuple on .NET Framework 4.6.2, so the pair is joined into one string.
            var known = (decisions ?? Enumerable.Empty<FuzzyDecision>())
                .GroupBy(d => d.GameId + "|" + d.EntryKey)
                .ToDictionary(g => g.Key, g => g.Last().Approved);

            var undecided = new List<GameMatch>();
            foreach (var match in matches.Where(m => m.Kind == MatchKind.Fuzzy))
            {
                if (known.TryGetValue(match.Game.Id + "|" + EntryKey(match.Entry), out var approved))
                {
                    match.Approved = approved;
                }
                else
                {
                    undecided.Add(match);
                }
            }

            return undecided;
        }

        /// <summary>Returns <paramref name="decisions"/> with the answers for <paramref name="reviewed"/> added or replaced.</summary>
        public static List<FuzzyDecision> Record(IEnumerable<FuzzyDecision> decisions, IEnumerable<GameMatch> reviewed)
        {
            var result = (decisions ?? Enumerable.Empty<FuzzyDecision>()).ToList();
            foreach (var match in reviewed)
            {
                var key = EntryKey(match.Entry);
                result.RemoveAll(d => d.GameId == match.Game.Id && d.EntryKey == key);
                result.Add(new FuzzyDecision { GameId = match.Game.Id, EntryKey = key, Approved = match.Approved });
            }

            return result;
        }
    }
}
