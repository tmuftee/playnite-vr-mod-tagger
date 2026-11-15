using System;
using System.Collections.Generic;
using System.Linq;
using VrModTagger.Applying;
using Xunit;

namespace VrModTagger.Tests
{
    public class TagHistoryTests
    {
        private static readonly TaggingOptions options = new TaggingOptions();
        private static readonly DateTime monday = new DateTime(2026, 11, 9, 20, 0, 0);
        private static readonly DateTime friday = new DateTime(2026, 11, 13, 21, 30, 0);

        private static LinkInfo Link(string name, string url = "https://pcvrgamer.net/x/") => new LinkInfo { Name = name, Url = url };

        private static GameChange Tagged(string game, params string[] tags) => new GameChange
        {
            GameId = Guid.NewGuid(),
            GameName = game,
            AddTags = tags.ToList(),
            AddLinks = { Link("VR Mod (PCVR Gamer)"), Link("Native VR (PCVR Central)", "https://pcvrcentral.com/games/x") }
        };

        [Fact]
        public void RecordsNewlyTaggedGameWithTagsAndSources()
        {
            var runId = Guid.NewGuid();
            var entry = Assert.Single(TagHistory.FromChanges(new[] { Tagged("Half-Life", "VR Mod", "VR Mod: Meta Quest") }, runId, monday, options));

            Assert.Equal(runId, entry.RunId);
            Assert.Equal(monday, entry.At);
            Assert.Equal("Half-Life", entry.GameName);
            Assert.False(entry.Removed);
            Assert.Equal(new[] { "VR Mod", "VR Mod: Meta Quest" }, entry.Tags);
            Assert.Equal(new[] { "PCVR Gamer", "PCVR Central" }, entry.Sources);
        }

        [Fact]
        public void RecordsUntaggedGame()
        {
            var change = new GameChange
            {
                GameId = Guid.NewGuid(), GameName = "Old Game",
                RemoveTags = { "VR Mod" },
                RemoveLinks = { Link("VR Mod (MrSurvivor)", "https://mrsurvivor-installers.com/#vr-hub") }
            };

            var entry = Assert.Single(TagHistory.FromChanges(new[] { change }, Guid.NewGuid(), monday, options));
            Assert.True(entry.Removed);
            Assert.Equal(new[] { "VR Mod" }, entry.Tags);
            Assert.Equal(new[] { "MrSurvivor" }, entry.Sources);
        }

        [Fact]
        public void ChangesWithOnlyLinksAreNotRecorded()
        {
            var change = new GameChange { GameId = Guid.NewGuid(), GameName = "Game", AddLinks = { Link("VR Mod (PCVR Gamer)") } };
            Assert.Empty(TagHistory.FromChanges(new[] { change }, Guid.NewGuid(), monday, options));
        }

        [Fact]
        public void TagsThatAreNotTheAddOnsAreIgnored()
        {
            var change = new GameChange { GameId = Guid.NewGuid(), GameName = "Game", AddTags = { "Favourites" } };
            Assert.Empty(TagHistory.FromChanges(new[] { change }, Guid.NewGuid(), monday, options));
        }

        [Fact]
        public void UsesConfiguredTagNames()
        {
            var custom = new TaggingOptions { VrModTagName = "Has VR mod" };
            var change = Tagged("Game", "Has VR mod", "VR Mod");
            Assert.Equal(new[] { "Has VR mod" }, Assert.Single(TagHistory.FromChanges(new[] { change }, Guid.NewGuid(), monday, custom)).Tags);
        }

        [Fact]
        public void SeedRecordsAlreadyTaggedGamesWithoutDate()
        {
            var games = new[]
            {
                new GameState { Id = Guid.NewGuid(), Name = "Zelda", TagNames = { "Native VR" }, Links = { Link("Native VR (PCVR Central)") } },
                new GameState { Id = Guid.NewGuid(), Name = "Alien: Isolation", TagNames = { "VR Mod", "Favourites" }, Links = { Link("VR Mod (MrSurvivor)"), Link("Store") } },
                new GameState { Id = Guid.NewGuid(), Name = "Untagged", TagNames = { "Favourites" } }
            };

            var seeded = TagHistory.Seed(games, options);
            Assert.Equal(new[] { "Alien: Isolation", "Zelda" }, seeded.Select(e => e.GameName));
            Assert.All(seeded, e => Assert.Null(e.At));
            Assert.All(seeded, e => Assert.Equal(Guid.Empty, e.RunId));
            Assert.Equal(new[] { "VR Mod" }, seeded[0].Tags);
            Assert.Equal(new[] { "MrSurvivor" }, seeded[0].Sources);
        }

        [Fact]
        public void UndoRemovesOnlyThatRun()
        {
            var history = new TagHistoryData();
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            TagHistory.Append(history, TagHistory.FromChanges(new[] { Tagged("A", "VR Mod") }, first, monday, options));
            TagHistory.Append(history, TagHistory.FromChanges(new[] { Tagged("B", "VR Mod"), Tagged("C", "VR Mod") }, second, friday, options));

            Assert.Equal(2, TagHistory.RemoveRun(history, second));
            Assert.Equal(new[] { "A" }, history.Entries.Select(e => e.GameName));
        }

        [Fact]
        public void UndoOfOldRunLogWithoutIdRemovesNothing()
        {
            var history = new TagHistoryData();
            TagHistory.Append(history, TagHistory.Seed(new[] { new GameState { Id = Guid.NewGuid(), Name = "A", TagNames = { "VR Mod" } } }, options));
            Assert.Equal(0, TagHistory.RemoveRun(history, Guid.Empty));
            Assert.Single(history.Entries);
        }

        [Fact]
        public void KeepsAtMostMaxEntriesDroppingOldest()
        {
            var history = new TagHistoryData();
            var many = Enumerable.Range(0, TagHistory.MaxEntries + 5)
                .Select(i => new HistoryEntry { GameName = "Game " + i, At = monday.AddMinutes(i), Tags = { "VR Mod" } });
            TagHistory.Append(history, many);

            Assert.Equal(TagHistory.MaxEntries, history.Entries.Count);
            Assert.Equal("Game 5", history.Entries.First().GameName);
        }

        [Fact]
        public void NewestFirstPutsUndatedLast()
        {
            var entries = new List<HistoryEntry>
            {
                new HistoryEntry { GameName = "Seeded B", At = null },
                new HistoryEntry { GameName = "Monday", At = monday },
                new HistoryEntry { GameName = "Seeded A", At = null },
                new HistoryEntry { GameName = "Friday 1", At = friday },
                new HistoryEntry { GameName = "Friday 2", At = friday }
            };

            Assert.Equal(new[] { "Friday 2", "Friday 1", "Monday", "Seeded A", "Seeded B" }, TagHistory.NewestFirst(entries).Select(e => e.GameName));
        }
    }
}
