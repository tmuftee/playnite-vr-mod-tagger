using System;
using System.Collections.Generic;
using System.Linq;
using VrModTagger.Applying;
using VrModTagger.Matching;
using VrModTagger.Models;
using Xunit;

namespace VrModTagger.Tests
{
    public class TagPlannerTests
    {
        private static readonly TaggingOptions defaults = new TaggingOptions();

        private static VrModEntry Mod(string source, string url, bool questOnly = false) => new VrModEntry
        {
            Source = source, Title = "Game", Url = url, QuestOnly = questOnly
        };

        private static VrModEntry Native(string url, bool alsoHasMod = false)
        {
            var entry = new VrModEntry { Source = "PCVR Central", Title = "Game", Url = url, NativeVr = true };
            if (alsoHasMod)
            {
                entry.ModNames.Add("Some mod");
            }

            return entry;
        }

        private static GameState Game(IEnumerable<string> tags = null, IEnumerable<LinkInfo> links = null) => new GameState
        {
            Id = Guid.NewGuid(),
            Name = "Game",
            TagNames = tags?.ToList() ?? new List<string>(),
            Links = links?.ToList() ?? new List<LinkInfo>()
        };

        private static LinkInfo Link(string name, string url) => new LinkInfo { Name = name, Url = url };

        private static List<GameMatch> Matches(GameState game, params VrModEntry[] entries) =>
            entries.Select(e => new GameMatch { Game = new LibraryGame { Id = game.Id, Name = game.Name }, Entry = e, Kind = MatchKind.Title }).ToList();

        private static GameChange PlanOne(GameState game, IEnumerable<GameMatch> matches, TaggingOptions options = null, ICollection<string> failed = null)
        {
            return TagPlanner.Plan(new[] { game }, matches, options ?? defaults, failed).SingleOrDefault();
        }

        [Fact]
        public void TagsModdedGameAndAddsOneLinkPerSource()
        {
            var game = Game();
            var change = PlanOne(game, Matches(game,
                Mod("PCVR Gamer", "https://pcvrgamer.net/game/"),
                Mod("MrSurvivor", "https://mrsurvivor-installers.com/#vr-hub")));

            Assert.Equal(new[] { "VR Mod" }, change.AddTags);
            Assert.Equal(new[] { "VR Mod (PCVR Gamer)", "VR Mod (MrSurvivor)" }, change.AddLinks.Select(l => l.Name));
            Assert.Empty(change.RemoveTags);
            Assert.Empty(change.RemoveLinks);
        }

        [Fact]
        public void NativeOnlyGameGetsOnlyNativeTag()
        {
            var game = Game();
            var change = PlanOne(game, Matches(game, Native("https://pcvrcentral.com/games/game")));
            Assert.Equal(new[] { "Native VR" }, change.AddTags);
            Assert.Equal("Native VR (PCVR Central)", Assert.Single(change.AddLinks).Name);
        }

        [Fact]
        public void NativeGameWithModGetsBothTags()
        {
            var game = Game();
            var change = PlanOne(game, Matches(game, Native("https://pcvrcentral.com/games/game", alsoHasMod: true)));
            Assert.Equal(new[] { "VR Mod", "Native VR" }, change.AddTags);
            Assert.Equal("VR Mod (PCVR Central)", Assert.Single(change.AddLinks).Name);
        }

        [Fact]
        public void NativeVrCanBeTurnedOff()
        {
            var game = Game();
            var options = new TaggingOptions { IncludeNativeVr = false };
            Assert.Null(PlanOne(game, Matches(game, Native("https://pcvrcentral.com/games/game")), options));
        }

        [Fact]
        public void QuestOnlyModIsLabelledAndCanGetItsOwnTag()
        {
            var game = Game();
            var matches = Matches(game, Mod("MrSurvivor", "https://mrsurvivor-installers.com/#vr-hub", questOnly: true));

            var change = PlanOne(game, matches);
            Assert.Equal(new[] { "VR Mod" }, change.AddTags);
            Assert.Equal("VR Mod (MrSurvivor, Meta Quest)", Assert.Single(change.AddLinks).Name);

            var withQuestTag = PlanOne(game, matches, new TaggingOptions { AddQuestTag = true });
            Assert.Equal(new[] { "VR Mod", "VR Mod: Meta Quest" }, withQuestTag.AddTags);
        }

        [Fact]
        public void PerSourceTags()
        {
            var game = Game();
            var change = PlanOne(game, Matches(game, Mod("PCVR Gamer", "a"), Mod("MrSurvivor", "b")), new TaggingOptions { AddPerSourceTags = true });
            Assert.Equal(new[] { "VR Mod", "VR Mod: PCVR Gamer", "VR Mod: MrSurvivor" }, change.AddTags);
        }

        [Fact]
        public void AlreadyTaggedGameHasNoChange()
        {
            var game = Game(new[] { "vr mod" }, new[] { Link("VR Mod (PCVR Gamer)", "https://pcvrgamer.net/game/") });
            Assert.Null(PlanOne(game, Matches(game, Mod("PCVR Gamer", "https://pcvrgamer.net/game/"))));
        }

        [Fact]
        public void LinkTheUserAddedIsNotDuplicated()
        {
            var game = Game(links: new[] { Link("My VR guide", "https://pcvrgamer.net/game/") });
            var change = PlanOne(game, Matches(game, Mod("PCVR Gamer", "https://pcvrgamer.net/game/")));
            Assert.Empty(change.AddLinks);
            Assert.Empty(change.RemoveLinks);
        }

        [Fact]
        public void SameUrlFromTwoEntriesGivesOneLink()
        {
            var game = Game();
            var change = PlanOne(game, Matches(game, Mod("PCVR Mods Installer Hub", "https://github.com/x"), Mod("PCVR Mods Installer Hub", "https://github.com/x")));
            Assert.Single(change.AddLinks);
        }

        [Fact]
        public void RenamesOwnedLinkWhenItsLabelChanges()
        {
            var game = Game(new[] { "VR Mod" }, new[] { Link("VR Mod (MrSurvivor)", "https://mrsurvivor-installers.com/#vr-hub") });
            var change = PlanOne(game, Matches(game, Mod("MrSurvivor", "https://mrsurvivor-installers.com/#vr-hub", questOnly: true)));
            Assert.Equal("VR Mod (MrSurvivor)", Assert.Single(change.RemoveLinks).Name);
            Assert.Equal("VR Mod (MrSurvivor, Meta Quest)", Assert.Single(change.AddLinks).Name);
            Assert.Empty(change.RemoveTags);
        }

        [Fact]
        public void RemovesStaleTagsAndOwnedLinksOnly()
        {
            var game = Game(
                new[] { "VR Mod", "Native VR", "VR Mod: PCVR Gamer", "Favorites" },
                new[]
                {
                    Link("VR Mod (PCVR Gamer)", "https://pcvrgamer.net/old/"),
                    Link("Native VR (PCVR Central)", "https://pcvrcentral.com/games/old"),
                    Link("Wiki", "https://example.com/wiki"),
                    Link("VR Mod guide", "https://example.com/guide")
                });

            var change = PlanOne(game, new List<GameMatch>());
            Assert.Equal(new[] { "VR Mod", "Native VR", "VR Mod: PCVR Gamer" }, change.RemoveTags);
            Assert.Equal(new[] { "VR Mod (PCVR Gamer)", "Native VR (PCVR Central)" }, change.RemoveLinks.Select(l => l.Name));
        }

        [Fact]
        public void RemovalCanBeTurnedOff()
        {
            var game = Game(new[] { "VR Mod" }, new[] { Link("VR Mod (PCVR Gamer)", "https://pcvrgamer.net/old/") });
            Assert.Null(PlanOne(game, new List<GameMatch>(), new TaggingOptions { RemoveStaleTags = false }));
        }

        [Fact]
        public void FailedSourceKeepsItsLinksAndAllTags()
        {
            var game = Game(
                new[] { "VR Mod" },
                new[]
                {
                    Link("VR Mod (PCVR Gamer)", "https://pcvrgamer.net/game/"),
                    Link("VR Mod (MrSurvivor)", "https://mrsurvivor-installers.com/#vr-hub")
                });

            // PCVR Gamer couldn't be fetched, MrSurvivor no longer lists the game.
            var change = PlanOne(game, new List<GameMatch>(), failed: new[] { "PCVR Gamer" });
            Assert.Empty(change.RemoveTags);
            Assert.Equal("VR Mod (MrSurvivor)", Assert.Single(change.RemoveLinks).Name);
        }

        [Fact]
        public void ExcludedGameLosesEverythingEvenWithRemovalOff()
        {
            var game = Game(new[] { "VR Mod" }, new[] { Link("VR Mod (PCVR Gamer)", "https://pcvrgamer.net/game/") });
            var options = new TaggingOptions { RemoveStaleTags = false, ExcludedGameIds = { game.Id } };
            var change = PlanOne(game, Matches(game, Mod("PCVR Gamer", "https://pcvrgamer.net/game/")), options, new[] { "PCVR Gamer" });
            Assert.Equal(new[] { "VR Mod" }, change.RemoveTags);
            Assert.Single(change.RemoveLinks);
            Assert.Empty(change.AddTags);
        }

        [Fact]
        public void FuzzyMatchesAreNotApplied()
        {
            var game = Game();
            var matches = Matches(game, Mod("PCVR Gamer", "a"));
            matches[0].Kind = MatchKind.Fuzzy;
            Assert.Null(PlanOne(game, matches));
        }

        [Fact]
        public void UsesConfiguredTagNames()
        {
            var game = Game();
            var options = new TaggingOptions { VrModTagName = "Has VR mod", NativeVrTagName = "VR built in" };
            var change = PlanOne(game, Matches(game, Native("u", alsoHasMod: true)), options);
            Assert.Equal(new[] { "Has VR mod", "VR built in" }, change.AddTags);
        }

        [Fact]
        public void UntouchedGamesAreLeftOut()
        {
            var tagged = Game();
            var untouched = Game(new[] { "Favorites" }, new[] { Link("Store", "https://store.example.com") });
            var changes = TagPlanner.Plan(new[] { tagged, untouched }, Matches(tagged, Mod("PCVR Gamer", "a")), defaults);
            Assert.Equal(new[] { tagged.Id }, changes.Select(c => c.GameId));
        }

        [Fact]
        public void InvertedChangeUndoesIt()
        {
            var change = new GameChange
            {
                AddTags = { "VR Mod" },
                RemoveTags = { "Native VR" },
                AddLinks = { Link("VR Mod (A)", "a") },
                RemoveLinks = { Link("VR Mod (B)", "b") }
            };

            var undo = change.Inverted();
            Assert.Equal(new[] { "Native VR" }, undo.AddTags);
            Assert.Equal(new[] { "VR Mod" }, undo.RemoveTags);
            Assert.Equal("b", Assert.Single(undo.AddLinks).Url);
            Assert.Equal("a", Assert.Single(undo.RemoveLinks).Url);
        }

        [Theory]
        [InlineData("VR Mod (PCVR Gamer)", true, "PCVR Gamer")]
        [InlineData("VR Mod (MrSurvivor, Meta Quest)", true, "MrSurvivor")]
        [InlineData("Native VR (PCVR Central)", true, "PCVR Central")]
        [InlineData("VR Mod guide", false, null)]
        [InlineData("Steam", false, null)]
        public void RecognizesOwnedLinks(string name, bool owned, string source)
        {
            var link = Link(name, "u");
            Assert.Equal(owned, TagPlanner.IsOwnedLink(link));
            if (owned)
            {
                Assert.Equal(source, TagPlanner.LinkSource(link));
            }
        }
    }
}
