using System;
using System.Linq;
using VrModTagger.Applying;
using Xunit;

namespace VrModTagger.Tests
{
    public class ChangePreviewTests
    {
        private static LinkInfo Link(string name, string url) => new LinkInfo { Name = name, Url = url };

        [Fact]
        public void ListsEachGameWithItsChanges()
        {
            var halfLife = new GameChange
            {
                GameId = Guid.NewGuid(),
                GameName = "Half-Life",
                AddTags = { "VR Mod", "VR Mod: Meta Quest" },
                AddLinks = { Link("VR Mod (PCVR Central)", "https://pcvrcentral.com/games/half-life"), Link("VR Mod (PCVR Gamer)", "https://pcvrgamer.net/half-life-1/") }
            };

            var row = Assert.Single(ChangePreview.Rows(new[] { halfLife }));
            Assert.Equal(halfLife.GameId, row.GameId);
            Assert.Equal("Half-Life", row.GameName);
            Assert.Equal("VR Mod, VR Mod: Meta Quest", row.AddedTags);
            Assert.Equal("", row.RemovedTags);
            Assert.Equal("PCVR Central, PCVR Gamer", row.AddedLinks);
            Assert.Equal("", row.RemovedLinks);
        }

        [Fact]
        public void TaggedGamesComeFirstThenUntaggedThenLinkOnly()
        {
            var changes = new[]
            {
                new GameChange { GameName = "Links only", AddLinks = { Link("VR Mod (MrSurvivor)", "https://mrsurvivor-installers.com/#vr-hub") } },
                new GameChange { GameName = "Untagged", RemoveTags = { "VR Mod" } },
                new GameChange { GameName = "B tagged", AddTags = { "VR Mod" } },
                new GameChange { GameName = "a tagged", AddTags = { "Native VR" } }
            };

            Assert.Equal(new[] { "a tagged", "B tagged", "Untagged", "Links only" }, ChangePreview.Rows(changes).Select(r => r.GameName));
        }

        [Fact]
        public void RenamedLinkIsNotListedAsAddedOrRemoved()
        {
            var url = "https://mrsurvivor-installers.com/#vr-hub";
            var change = new GameChange
            {
                GameName = "Game",
                RemoveLinks = { Link("VR Mod (MrSurvivor)", url), Link("VR Mod (PCVR Gamer)", "https://pcvrgamer.net/old/") },
                AddLinks = { Link("VR Mod (MrSurvivor, Meta Quest)", url) }
            };

            var row = Assert.Single(ChangePreview.Rows(new[] { change }));
            Assert.Equal("", row.AddedLinks);
            Assert.Equal("PCVR Gamer", row.RemovedLinks);
        }

        [Theory]
        [InlineData(1, "1 game gets")]
        [InlineData(3, "3 games get")]
        public void CountsReadNaturally(int count, string expected)
        {
            Assert.Equal(expected, ChangePreview.Games(count, "gets", "get"));
        }
    }
}
