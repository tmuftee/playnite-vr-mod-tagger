using System;
using System.Collections.Generic;
using System.Linq;
using VrModTagger.Matching;
using VrModTagger.Models;
using VrModTagger.Sources;
using Xunit;

namespace VrModTagger.Tests
{
    /// <summary>Matching against the saved data of all four sources, with a made-up library.</summary>
    public class RealDataMatchingTests
    {
        private static readonly Lazy<GameMatcher> matcher = new Lazy<GameMatcher>(() =>
        {
            var entries = new List<VrModEntry>();
            entries.AddRange(MrSurvivorParser.Parse(Fixture.Read("mrsurvivor.html")));
            entries.AddRange(new[] { "pcvrgamer-page1.json", "pcvrgamer-page2.json" }
                .SelectMany(f => PcvrGamerParser.ParsePosts(Fixture.Read(f)))
                .Select(PcvrGamerParser.ToEntry));
            entries.AddRange(PcvrCentralParser.ToEntries(
                PcvrCentralParser.ParseGames(Fixture.Read("pcvrcentral-mods.html"))
                    .Concat(PcvrCentralParser.ParseGames(Fixture.Read("pcvrcentral-native-vr.html")))));

            var hub = new[] { "AlienIsolationVR", "7DaysVR", "ACMirageVR", "AlanWakeVR", "AmnesiaVR", "BlackMesaVR", "DMC5VR", "Fallout3VR" }
                .Select(f => InstallerHubParser.ParseScript(f, Fixture.Read(f + "-core.ps1")))
                .ToList();
            InstallerHubParser.AddReadmeTitles(hub, Fixture.Read("hub-README.md"));
            entries.AddRange(hub);
            return new GameMatcher(entries);
        });

        private static List<GameMatch> Match(string name, uint? steamAppId = null, int? year = null)
        {
            return matcher.Value.Match(new LibraryGame { Id = Guid.NewGuid(), Name = name, SteamAppId = steamAppId, ReleaseYear = year });
        }

        private static List<string> Sources(List<GameMatch> matches) => matches.Select(m => m.Entry.Source).Distinct().OrderBy(s => s).ToList();

        [Fact]
        public void AlienIsolationOnSteamIsFoundInEverySourceThatListsIt()
        {
            var matches = Match("Alien: Isolation", 214490);
            Assert.Contains(MrSurvivorSource.SourceName, Sources(matches));
            Assert.Contains(InstallerHubSource.SourceName, Sources(matches));
            Assert.Contains(PcvrCentralSource.SourceName, Sources(matches));
            Assert.Contains(matches, m => m.Kind == MatchKind.SteamAppId);
        }

        [Fact]
        public void EpicCopyIsFoundByTitle()
        {
            var matches = Match("Dead Island 2");
            Assert.Contains(MrSurvivorSource.SourceName, Sources(matches));
            Assert.Contains(PcvrGamerSource.SourceName, Sources(matches));
            Assert.All(matches, m => Assert.Equal(MatchKind.Title, m.Kind));
        }

        [Fact]
        public void AbbreviatedPcvrGamerTitleMatchesThroughBuyLinkName()
        {
            var matches = Match("Red Dead Redemption");
            Assert.Contains(matches, m => m.Entry.Source == PcvrGamerSource.SourceName && m.Entry.Title == "R.D.R.");
        }

        [Fact]
        public void QuestOnlyModMatches()
        {
            var matches = Match("Half-Life 2: Episode One");
            Assert.Contains(matches, m => m.Entry.Source == MrSurvivorSource.SourceName && m.Entry.QuestOnly);
        }

        [Fact]
        public void NativeVrGameMatchesBySteamId()
        {
            var match = Assert.Single(Match("1943 Berlin Blitz", 513490));
            Assert.True(match.Entry.NativeVr);
        }

        [Fact]
        public void FlatFalloutFourIsNotTaggedNativeVr()
        {
            Assert.DoesNotContain(Match("Fallout 4", 377160), m => m.Entry.NativeVr && m.Entry.ModNames.Count == 0);
        }

        [Fact]
        public void UnrelatedGameHasNoMatch()
        {
            Assert.Empty(Match("Some Game Nobody Modded 12345"));
        }
    }
}
