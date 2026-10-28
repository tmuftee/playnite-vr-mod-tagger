using System;
using System.Collections.Generic;
using System.Linq;
using VrModTagger.Matching;
using VrModTagger.Models;
using Xunit;

namespace VrModTagger.Tests
{
    public class GameMatcherTests
    {
        private static VrModEntry Mod(string title, params uint[] appIds) => new VrModEntry
        {
            Source = "Test", Title = title, Url = "https://example.com/" + title, ModNames = { "Some mod" }, SteamAppIds = appIds.ToList()
        };

        private static VrModEntry Native(string title, params uint[] appIds) => new VrModEntry
        {
            Source = "Test", Title = title, Url = "https://example.com/" + title, NativeVr = true, SteamAppIds = appIds.ToList()
        };

        private static LibraryGame Game(string name, uint? steamAppId = null, int? year = null, params uint[] linkedAppIds) => new LibraryGame
        {
            Id = Guid.NewGuid(), Name = name, SteamAppId = steamAppId, ReleaseYear = year, LinkedSteamAppIds = linkedAppIds.ToList()
        };

        private static List<GameMatch> Match(LibraryGame game, params VrModEntry[] entries) => new GameMatcher(entries).Match(game);

        [Fact]
        public void MatchesSteamGameByAppId()
        {
            var entry = Mod("Totally Different Title", 214490);
            var match = Assert.Single(Match(Game("Alien: Isolation", 214490), entry));
            Assert.Equal(MatchKind.SteamAppId, match.Kind);
            Assert.Same(entry, match.Entry);
        }

        [Fact]
        public void MatchesNonSteamCopyByStoreLink()
        {
            var match = Assert.Single(Match(Game("Cyberpunk 2077 (GOG)", null, null, 1091500), Mod("CP77", 1091500)));
            Assert.Equal(MatchKind.SteamLink, match.Kind);
        }

        [Theory]
        [InlineData("7 Days to Die VR (motion controls)", "7 Days To Die")]
        [InlineData("Halo MCC VR", "Halo: The Master Chief Collection", "Halo: The Master Chief Collection")]
        [InlineData("R.D.R.", "Red Dead Redemption", "Red Dead Redemption")]
        [InlineData("Bendy & The Ink Machine", "Bendy and the Ink Machine")]
        [InlineData("Borderlands GOTY Edition", "Borderlands Game of the Year Enhanced")]
        [InlineData("Diablo II: Resurrected — D2R VR Unleashed", "Diablo® II: Resurrected™")]
        public void MatchesByTitle(string entryTitle, string gameName, string alternateTitle = null)
        {
            var entry = Mod(entryTitle);
            if (alternateTitle != null)
            {
                entry.AlternateTitles.Add(alternateTitle);
            }

            var match = Assert.Single(Match(Game(gameName), entry));
            Assert.Equal(MatchKind.Title, match.Kind);
        }

        [Fact]
        public void FirstGameNumberedOneMatchesStoreName()
        {
            Assert.Single(Match(Game("Half-Life"), Mod("Half Life 1")));
            Assert.Single(Match(Game("Battlefield 1"), Mod("Battlefield 1 VR")));
        }

        [Fact]
        public void DoesNotMatchDifferentYear()
        {
            var prey2006 = Mod("Prey (2006)");
            Assert.Empty(Match(Game("Prey", 480490, 2017), prey2006));
            Assert.Single(Match(Game("Prey", null, 2006), prey2006));
            Assert.Single(Match(Game("Prey"), prey2006));
        }

        [Fact]
        public void DoesNotMatchTitleWhenSteamIdsDisagree()
        {
            // Same name, but the mod is for a different Steam app (e.g. a remaster).
            Assert.Empty(Match(Game("BioShock", 7670), Mod("BioShock", 409710)));
        }

        [Fact]
        public void TitleMatchStillWorksWhenOnlyOneSideHasSteamId()
        {
            Assert.Single(Match(Game("Dishonored 2", 403640), Mod("Dishonored 2")));
            Assert.Single(Match(Game("Dishonored 2"), Mod("Dishonored 2", 403640)));
        }

        [Fact]
        public void NativeVrGameDoesNotMatchFlatEdition()
        {
            var f4vr = Native("Fallout 4 VR", 611660);
            Assert.Empty(Match(Game("Fallout 4"), f4vr));
            Assert.Empty(Match(Game("Fallout 4", 377160), f4vr));
            Assert.Single(Match(Game("Fallout 4 VR"), f4vr));
            Assert.Single(Match(Game("Fallout 4 VR", 611660), f4vr));
        }

        [Fact]
        public void ModTitleWithVrMatchesFlatGame()
        {
            Assert.Single(Match(Game("Dishonored"), Mod("Dishonored VR")));
        }

        [Fact]
        public void ReturnsEveryMatchingEntryOnce()
        {
            var a = Mod("Cyberpunk 2077", 1091500);
            var b = Mod("Cyberpunk 2077 VR");
            b.Source = "Other";
            var matches = Match(Game("Cyberpunk 2077", 1091500), a, b);
            Assert.Equal(2, matches.Count);
            Assert.Equal(MatchKind.SteamAppId, matches.Single(m => m.Entry == a).Kind);
            Assert.Equal(MatchKind.Title, matches.Single(m => m.Entry == b).Kind);
        }

        [Fact]
        public void FuzzyMatchingIsOffByDefault()
        {
            Assert.Empty(Match(Game("Deus Ex: Human Revolution"), Mod("Deux Ex: Human Revolution")));
        }

        [Fact]
        public void FuzzyMatchingFindsTyposWhenEnabled()
        {
            var matcher = new GameMatcher(new[] { Mod("Deux Ex: Human Revolution") }, enableFuzzy: true, fuzzyThreshold: 0.95);
            var match = Assert.Single(matcher.Match(Game("Deus Ex: Human Revolution")));
            Assert.Equal(MatchKind.Fuzzy, match.Kind);
            Assert.True(match.Score >= 0.95);
        }

        [Fact]
        public void FuzzyMatchingIgnoresShortTitles()
        {
            var matcher = new GameMatcher(new[] { Mod("Doom 3") }, enableFuzzy: true, fuzzyThreshold: 0.8);
            Assert.Empty(matcher.Match(Game("Doom 2")));
        }

        [Theory]
        [InlineData("Titanfall", "Titanfall 2")]
        [InlineData("Forza Horizon 4", "Forza Horizon 5")]
        [InlineData("Call of Duty: Black Ops II", "Call of Duty: Black Ops")]
        public void FuzzyMatchingDoesNotConfuseSequels(string gameName, string entryTitle)
        {
            var matcher = new GameMatcher(new[] { Mod(entryTitle) }, enableFuzzy: true, fuzzyThreshold: 0.9);
            Assert.Empty(matcher.Match(Game(gameName)));
        }

        [Fact]
        public void FuzzyMatchingFindsMisspelling()
        {
            var matcher = new GameMatcher(new[] { Mod("Medal Of Honor: Airborn") }, enableFuzzy: true, fuzzyThreshold: 0.95);
            Assert.Equal(MatchKind.Fuzzy, Assert.Single(matcher.Match(Game("Medal of Honor: Airborne"))).Kind);
        }

        [Fact]
        public void FuzzyMatchingSkipsGamesWithExactMatch()
        {
            var matcher = new GameMatcher(new[] { Mod("Half-Life 2"), Mod("Half-Life 2: Episode 1") }, enableFuzzy: true, fuzzyThreshold: 0.8);
            var match = Assert.Single(matcher.Match(Game("Half-Life 2")));
            Assert.Equal(MatchKind.Title, match.Kind);
        }

        [Theory]
        [InlineData("martha", "marhta", 0.961)]
        [InlineData("dwayne", "duane", 0.84)]
        [InlineData("abc", "xyz", 0)]
        public void JaroWinklerKnownValues(string a, string b, double expected)
        {
            Assert.Equal(expected, GameMatcher.JaroWinkler(a, b), 2);
        }
    }
}
