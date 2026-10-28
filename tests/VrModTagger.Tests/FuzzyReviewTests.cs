using System;
using System.Collections.Generic;
using System.Linq;
using VrModTagger.Applying;
using VrModTagger.Matching;
using VrModTagger.Models;
using Xunit;

namespace VrModTagger.Tests
{
    public class FuzzyReviewTests
    {
        private static GameMatch Fuzzy(Guid gameId, string title) => new GameMatch
        {
            Game = new LibraryGame { Id = gameId, Name = "Medal of Honor: Airborne" },
            Entry = new VrModEntry { Source = "PCVR Gamer", Title = title, Url = "https://pcvrgamer.net/x/" },
            Kind = MatchKind.Fuzzy,
            Score = 0.99
        };

        [Fact]
        public void NewSuggestionsAreUndecidedAndNotApplied()
        {
            var match = Fuzzy(Guid.NewGuid(), "Medal Of Honor: Airborn");
            var undecided = FuzzyReview.ApplyDecisions(new[] { match }, new List<FuzzyDecision>());
            Assert.Same(match, Assert.Single(undecided));
            Assert.False(match.IsApplied);
        }

        [Fact]
        public void RecordedAnswersAreRemembered()
        {
            var gameId = Guid.NewGuid();
            var accepted = Fuzzy(gameId, "Medal Of Honor: Airborn");
            var rejected = Fuzzy(gameId, "Medal of Honour Airborne");
            accepted.Approved = true;
            var decisions = FuzzyReview.Record(null, new[] { accepted, rejected });

            // Next run: fresh match objects for the same pairs.
            var again = new[] { Fuzzy(gameId, "Medal Of Honor: Airborn"), Fuzzy(gameId, "Medal of Honour Airborne") };
            Assert.Empty(FuzzyReview.ApplyDecisions(again, decisions));
            Assert.True(again[0].IsApplied);
            Assert.False(again[1].IsApplied);
        }

        [Fact]
        public void ChangingAnAnswerReplacesTheOldOne()
        {
            var match = Fuzzy(Guid.NewGuid(), "Medal Of Honor: Airborn");
            match.Approved = true;
            var decisions = FuzzyReview.Record(null, new[] { match });
            match.Approved = false;
            decisions = FuzzyReview.Record(decisions, new[] { match });

            Assert.False(Assert.Single(decisions).Approved);
        }

        [Fact]
        public void AnswerIsPerGame()
        {
            var match = Fuzzy(Guid.NewGuid(), "Medal Of Honor: Airborn");
            match.Approved = true;
            var decisions = FuzzyReview.Record(null, new[] { match });

            var otherGame = Fuzzy(Guid.NewGuid(), "Medal Of Honor: Airborn");
            Assert.Single(FuzzyReview.ApplyDecisions(new[] { otherGame }, decisions));
        }

        [Fact]
        public void ExactMatchesAreAlwaysApplied()
        {
            var match = Fuzzy(Guid.NewGuid(), "x");
            match.Kind = MatchKind.Title;
            Assert.True(match.IsApplied);
            Assert.Empty(FuzzyReview.ApplyDecisions(new[] { match }, null));
        }

        [Fact]
        public void PlannerTagsApprovedSuggestion()
        {
            var game = new GameState { Id = Guid.NewGuid(), Name = "Medal of Honor: Airborne" };
            var match = Fuzzy(game.Id, "Medal Of Honor: Airborn");
            match.Approved = true;

            var change = Assert.Single(TagPlanner.Plan(new[] { game }, new[] { match }, new TaggingOptions()));
            Assert.Equal(new[] { "VR Mod" }, change.AddTags);
        }
    }
}
