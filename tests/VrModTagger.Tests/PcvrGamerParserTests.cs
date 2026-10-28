using System.Collections.Generic;
using System.Linq;
using VrModTagger.Models;
using VrModTagger.Sources;
using Xunit;

namespace VrModTagger.Tests
{
    public class PcvrGamerParserTests
    {
        private static readonly List<VrModEntry> entries = new[] { "pcvrgamer-page1.json", "pcvrgamer-page2.json" }
            .SelectMany(f => PcvrGamerParser.ParsePosts(Fixture.Read(f)))
            .Select(PcvrGamerParser.ToEntry)
            .ToList();

        private static VrModEntry Entry(string url) => entries.Single(e => e.Url == url);

        [Fact]
        public void ParsesEveryModPost()
        {
            // A sample of the 106 mod posts on 2026-10-28, with only the paragraphs around Steam links kept.
            Assert.Equal(15, entries.Count);
            Assert.All(entries, e =>
            {
                Assert.Equal(PcvrGamerSource.SourceName, e.Source);
                Assert.StartsWith("https://pcvrgamer.net/", e.Url);
                Assert.False(string.IsNullOrWhiteSpace(e.Title));
            });
        }

        [Fact]
        public void DecodesTitles()
        {
            Assert.Equal("Hogwart’s Legacy", Entry("https://pcvrgamer.net/hogwarts-legacy/").Title);
            Assert.Equal("Bendy & The Ink Machine", Entry("https://pcvrgamer.net/bendy-the-ink-machine/").Title);
        }

        [Fact]
        public void ReadsBuyLink()
        {
            var deadSpace = Entry("https://pcvrgamer.net/dead-space-2/");
            Assert.Equal(new uint[] { 47780 }, deadSpace.SteamAppIds);
        }

        [Fact]
        public void IgnoresModdersOwnGameLink()
        {
            // "Support sqofer's work by buying his VR game on Steam HERE" links CYBRID (1636850).
            var nfs = Entry("https://pcvrgamer.net/need-for-speed-most-wanted/");
            Assert.Equal(new uint[] { 1262560 }, nfs.SteamAppIds);
        }

        [Fact]
        public void KeepsFullNameForAbbreviatedTitle()
        {
            var rdr = Entry("https://pcvrgamer.net/red-dead-redemption/");
            Assert.Equal("R.D.R.", rdr.Title);
            Assert.Equal(new uint[] { 2668510 }, rdr.SteamAppIds);
            Assert.Contains("Red Dead Redemption", rdr.AlternateTitles);
        }

        [Fact]
        public void ReadsListOfSupportedGames()
        {
            // RAZE XR supports six games; five are linked on Steam, Duke Nukem 3D on zoom-platform.
            var raze = Entry("https://pcvrgamer.net/raze-xr/");
            Assert.Equal(new uint[] { 1010750, 238070, 1678430, 329650, 376750 }, raze.SteamAppIds);
            Assert.Contains("BLOOD: Fresh Supply", raze.AlternateTitles);
            Assert.Contains("World War II GI", raze.AlternateTitles);
        }

        [Fact]
        public void AcceptsSteamPageOfTheModItself()
        {
            // "GET THE MOD HERE" links the Half-Life 2: VR Mod Steam app, whose URL slug matches the title.
            Assert.Equal(new uint[] { 658920 }, Entry("https://pcvrgamer.net/half-life-2/").SteamAppIds);
        }

        [Fact]
        public void HandlesAgeCheckLinkWithoutSlug()
        {
            // "But Dishonored 2 on Steam HERE" (sic) -> store.steampowered.com/agecheck/app/403640/
            Assert.Equal(new uint[] { 403640 }, Entry("https://pcvrgamer.net/dishonored-2/").SteamAppIds);
        }

        [Fact]
        public void GenericAnchorTextIsNotUsedAsGameName()
        {
            // "BUY THE GAME ON STEAM HERE": the name comes from the store slug instead.
            var repo = Entry("https://pcvrgamer.net/r-e-p-o/");
            Assert.Equal(new uint[] { 3241660 }, repo.SteamAppIds);
            Assert.DoesNotContain(entries.SelectMany(e => e.AlternateTitles), t => t == "THE GAME");
        }

        [Fact]
        public void ParsesEmptyPage()
        {
            Assert.Empty(PcvrGamerParser.ParsePosts("[]"));
        }

        [Fact]
        public void SupportLinksNextToBuyLinksAreIgnored()
        {
            const string html =
                "<p>Buy Some Game on Steam <a href=\"https://store.steampowered.com/app/100/Some_Game/\">HERE</a></p>" +
                "<p>Support the modder by donating <a href=\"https://store.steampowered.com/app/200/Other_Game/\">HERE</a></p>";
            var links = PcvrGamerParser.FindGameSteamLinks("Some Game", html);
            Assert.Equal(new uint[] { 100 }, links.Select(l => l.AppId));
            Assert.Equal("Some Game", links[0].GameName);
        }
    }
}
