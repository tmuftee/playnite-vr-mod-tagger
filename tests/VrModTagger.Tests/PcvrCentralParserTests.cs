using System;
using System.Collections.Generic;
using System.Linq;
using VrModTagger.Models;
using VrModTagger.Sources;
using Xunit;

namespace VrModTagger.Tests
{
    public class PcvrCentralParserTests
    {
        private static readonly List<PcvrCentralGame> modGames = PcvrCentralParser.ParseGames(Fixture.Read("pcvrcentral-mods.html"));
        private static readonly List<PcvrCentralGame> nativeGames = PcvrCentralParser.ParseGames(Fixture.Read("pcvrcentral-native-vr.html"));
        private static readonly List<VrModEntry> entries = PcvrCentralParser.ToEntries(modGames.Concat(nativeGames));

        private static VrModEntry Entry(string slug) => entries.Single(e => e.Url == "https://pcvrcentral.com/games/" + slug);

        [Fact]
        public void ParsesBothCatalogs()
        {
            // The fixtures are samples of the real catalogs (1,122 and 3,369 games on 2026-10-28), split over two page-data chunks.
            Assert.Equal(39, modGames.Count);
            Assert.Equal(22, nativeGames.Count);
        }

        [Fact]
        public void MergesGamesListedOnBothPages()
        {
            Assert.Equal(entries.Count, entries.Select(e => e.Url).Distinct().Count());
            var both = modGames.Select(g => g.Slug).Intersect(nativeGames.Select(g => g.Slug)).ToList();
            Assert.NotEmpty(both);
            Assert.All(both, slug =>
            {
                var entry = Entry(slug);
                Assert.True(entry.NativeVr);
            });
        }

        [Fact]
        public void ReadsModdedGame()
        {
            var cyberpunk = Entry("cyberpunk-2077");
            Assert.Equal("Cyberpunk 2077", cyberpunk.Title);
            Assert.Equal(PcvrCentralSource.SourceName, cyberpunk.Source);
            Assert.Equal(new uint[] { 1091500 }, cyberpunk.SteamAppIds);
            Assert.Contains(cyberpunk.ModNames, m => m.Contains("Luke Ross"));
            Assert.False(cyberpunk.NativeVr);
            Assert.False(cyberpunk.QuestOnly);
        }

        [Fact]
        public void ReadsNativeVrGame()
        {
            var game = Entry("1943-berlin-blitz");
            Assert.True(game.NativeVr);
            Assert.Equal(new uint[] { 513490 }, game.SteamAppIds);
            Assert.Empty(game.ModNames);
        }

        [Fact]
        public void UndefinedSteamAppIdIsLeftEmpty()
        {
            // Games not on Steam have steamAppId "$undefined".
            var noSteam = modGames.Where(g => g.SteamAppId == null).ToList();
            Assert.True(noSteam.Count >= 5, $"Only {noSteam.Count} games without a Steam AppID.");
            Assert.All(noSteam, g => Assert.Empty(Entry(g.Slug).SteamAppIds));
        }

        [Fact]
        public void DecodesTitles()
        {
            Assert.Contains(entries, e => e.Title == "Black & White (2001)");
            Assert.DoesNotContain(entries, e => e.Title.StartsWith("$", StringComparison.Ordinal));
        }

        [Fact]
        public void FlagsQuestOnlyMods()
        {
            var questOnly = entries.Where(e => e.QuestOnly).ToList();
            Assert.NotEmpty(questOnly);
            Assert.All(questOnly, e => Assert.DoesNotContain("pcvr", e.Platforms));
        }

        [Fact]
        public void LeavesOutBrokenMods()
        {
            var games = new[]
            {
                new PcvrCentralGame
                {
                    Slug = "broken-only", Title = "Broken Only", NativeVr = "none",
                    Mods = { new PcvrCentralMod { Name = "Old mod", Status = "broken", Platforms = { "pcvr" } } }
                },
                new PcvrCentralGame
                {
                    Slug = "mixed", Title = "Mixed", NativeVr = "none",
                    Mods =
                    {
                        new PcvrCentralMod { Name = "Old mod", Status = "broken", Platforms = { "pcvr" } },
                        new PcvrCentralMod { Name = "New mod", Author = "Someone", Status = "abandoned", Platforms = { "pcvr" } }
                    }
                }
            };

            var result = PcvrCentralParser.ToEntries(games);
            Assert.Equal(new[] { "Mixed" }, result.Select(e => e.Title));
            Assert.Equal(new[] { "New mod by Someone" }, result[0].ModNames);
        }

        [Fact]
        public void CanLeaveOutNativeVr()
        {
            var withoutNative = PcvrCentralParser.ToEntries(modGames, includeNativeVr: false);
            Assert.DoesNotContain(withoutNative, e => e.NativeVr);
            Assert.Equal(modGames.Count(g => g.Mods.Any(m => m.Status != "broken")), withoutNative.Count);
        }

        [Fact]
        public void ThrowsWhenCatalogIsMissing()
        {
            Assert.Throws<FormatException>(() => PcvrCentralParser.ParseGames("<html><script>self.__next_f.push([1,\"nothing here\"])</script></html>"));
        }
    }
}
