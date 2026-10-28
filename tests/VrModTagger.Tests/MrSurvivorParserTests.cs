using System;
using System.Linq;
using VrModTagger.Sources;
using Xunit;

namespace VrModTagger.Tests
{
    public class MrSurvivorParserTests
    {
        private readonly System.Collections.Generic.List<Models.VrModEntry> entries = MrSurvivorParser.Parse(Fixture.Read("mrsurvivor.html"));

        [Fact]
        public void ParsesEveryRow()
        {
            Assert.Equal(69, entries.Count);
            Assert.All(entries, e =>
            {
                Assert.Equal(MrSurvivorSource.SourceName, e.Source);
                Assert.Equal(MrSurvivorSource.LinkUrl, e.Url);
                Assert.False(string.IsNullOrWhiteSpace(e.Title));
                Assert.NotEmpty(e.Platforms);
            });
        }

        [Fact]
        public void SplitsPlatforms()
        {
            var crysis = entries.Single(e => e.Title == "Crysis");
            Assert.Equal(new[] { "Steam legacy", "GOG", "Manual Detection" }, crysis.Platforms);
            Assert.False(crysis.QuestOnly);
        }

        [Theory]
        [InlineData("Half-Life 2: Episode One")]
        [InlineData("Aliens Versus Predator Quest")]
        [InlineData("GTA San Andreas VR")]
        [InlineData("Galaxy Quest — Super Mario Galaxy")]
        public void KeepsQuestOnlyRows(string title)
        {
            var entry = entries.Single(e => e.Title == title);
            Assert.True(entry.QuestOnly);
            Assert.True(entry.HasQuestPort);
        }

        [Fact]
        public void PcRowsHaveNoQuestPort()
        {
            Assert.False(entries.Single(e => e.Title == "Crysis").HasQuestPort);
        }

        [Theory]
        [InlineData("GoldenEye 007 VR — PCVR")]
        [InlineData("Alien Isolation")]
        [InlineData("Diablo II: Resurrected — D2R VR Unleashed")]
        public void PcRowsAreNotQuestOnly(string title)
        {
            Assert.False(entries.Single(e => e.Title == title).QuestOnly);
        }

        [Fact]
        public void ReadsGameNameFromPlatformNote()
        {
            var halo = entries.Single(e => e.Title == "Halo MCC VR");
            Assert.Equal(new[] { "Steam" }, halo.Platforms);
            Assert.Contains("Halo: The Master Chief Collection", halo.AlternateTitles);
        }

        [Fact]
        public void ReadsTextAfterDashAsAlternateTitle()
        {
            Assert.Contains("Super Mario Galaxy", entries.Single(e => e.Title == "Galaxy Quest — Super Mario Galaxy").AlternateTitles);
        }

        [Fact]
        public void OneWordAfterDashIsAModNameNotATitle()
        {
            var cod = entries.Single(e => e.Title.EndsWith("— Overlord", StringComparison.Ordinal));
            Assert.Contains("Overlord", cod.ModNames);
            Assert.DoesNotContain("Overlord", cod.AlternateTitles);
        }

        [Fact]
        public void PlatformAfterDashIsNotATitle()
        {
            Assert.Empty(entries.Single(e => e.Title == "GoldenEye 007 VR — PCVR").AlternateTitles);
            Assert.Empty(entries.Single(e => e.Title == "GoldenEye 007 VR — Meta Quest").AlternateTitles);
        }

        [Fact]
        public void FlagsTemporarilyUnavailableRows()
        {
            var avp = entries.Single(e => e.Title == "Aliens Versus Predator Classic");
            Assert.True(avp.TemporarilyUnavailable);
            Assert.Equal(new[] { "Steam", "GOG", "Retail (Manual)" }, avp.Platforms);
            Assert.Empty(avp.AlternateTitles);
        }

        [Fact]
        public void DecodesHtmlEntities()
        {
            Assert.Contains(entries, e => e.Title == "Marvel's Spider-Man Remastered VR — SpidyVR");
            Assert.Contains(entries, e => e.Title == "The Simpsons: Hit & Run VR");
            Assert.DoesNotContain(entries, e => e.Title.Contains("&amp;") || e.Title.Contains("&#"));
        }

        [Fact]
        public void ThrowsWhenTableIsMissing()
        {
            Assert.Throws<FormatException>(() => MrSurvivorParser.Parse("<html><body>No table</body></html>"));
        }
    }
}
