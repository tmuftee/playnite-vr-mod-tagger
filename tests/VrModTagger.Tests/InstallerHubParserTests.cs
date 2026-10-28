using System;
using System.Collections.Generic;
using System.Linq;
using VrModTagger.Models;
using VrModTagger.Sources;
using Xunit;

namespace VrModTagger.Tests
{
    public class InstallerHubParserTests
    {
        [Fact]
        public void ParseTreeFindsInstallerScripts()
        {
            var tree = InstallerHubParser.ParseTree(Fixture.Read("hub-tree.json"));
            Assert.False(string.IsNullOrEmpty(tree.Sha));
            Assert.True(tree.Scripts.Count > 250, $"Only {tree.Scripts.Count} scripts found.");
            var alien = tree.Scripts.Single(s => s.Folder == "AlienIsolationVR");
            Assert.Equal("Core/AlienIsolationVR/AlienIsolationVR-core.ps1", alien.Path);
            Assert.DoesNotContain(tree.Scripts, s => s.Folder == "Assets");
        }

        [Theory]
        [InlineData("AlienIsolationVR", "Alien: Isolation VR", new uint[] { 214490 })]   // -AppId "214490", WindowTitle "... Mod Installer"
        [InlineData("7DaysVR", "7 Days to Die VR", new uint[] { 251570 })]
        [InlineData("ACMirageVR", "Assassin's Creed Mirage VR", new uint[] { 3035570 })] // $STEAM_APPID = "3035570"
        [InlineData("AlanWakeVR", "Alan Wake VR", new uint[] { 108710 })]                // single quotes
        [InlineData("BlackMesaVR", "Black Mesa VR", new uint[] { 362890 })]              // $APP_ID = '362890'
        [InlineData("AmnesiaVR", "Amnesia VR", new uint[] { 57300 })]                    // $GAME_APPID, title from -GameName
        [InlineData("Fallout3VR", "Fallout 3 VR", new uint[] { 22370, 22300 })]          // foreach over several AppIDs
        [InlineData("DMC5VR", "Devil May Cry 5 VR", new uint[0])]                        // shared installer, no AppID in script
        public void ParseScriptReadsTitleAndAppIds(string folder, string title, uint[] appIds)
        {
            var entry = InstallerHubParser.ParseScript(folder, Fixture.Read($"{folder}-core.ps1"));
            Assert.Equal(InstallerHubSource.SourceName, entry.Source);
            Assert.Equal(title, entry.Title);
            Assert.Equal(appIds, entry.SteamAppIds);
            Assert.Equal($"https://github.com/Mr-Nlce/PCVR-Mods-Installer-Hub/tree/HEAD/Core/{folder}", entry.Url);
        }

        [Fact]
        public void ParseScriptReadsSteamFolderNamesAsAlternateTitles()
        {
            var entry = InstallerHubParser.ParseScript("Fallout3VR", Fixture.Read("Fallout3VR-core.ps1"));
            Assert.Contains("Fallout 3 goty", entry.AlternateTitles);
            Assert.Contains("Fallout 3", entry.AlternateTitles);
        }

        [Fact]
        public void ParseScriptKeepsOtherScriptTitlesAsAlternateTitles()
        {
            var entry = InstallerHubParser.ParseScript("AmnesiaVR", Fixture.Read("AmnesiaVR-core.ps1"));
            Assert.Contains("Amnesia The Dark Descent", entry.AlternateTitles);
            Assert.DoesNotContain(entry.AlternateTitles, t => t.Contains("$"));
        }

        [Fact]
        public void IgnoresSteamVrAppId()
        {
            var ids = InstallerHubParser.FindSteamAppIds("Find-SteamGameFolder -AppId '250820'\n$APP_ID = '22330'");
            Assert.Equal(new uint[] { 22330 }, ids);
        }

        [Fact]
        public void FindsAppManifestIds()
        {
            Assert.Equal(new uint[] { 620 }, InstallerHubParser.FindSteamAppIds("Join-Path $steamapps 'appmanifest_620.acf'"));
        }

        [Theory]
        [InlineData("AlienIsolationVR", "Alien Isolation VR")]
        [InlineData("Bioshock2VR", "Bioshock 2 VR")]
        [InlineData("DMC5VR", "DMC 5 VR")]
        public void TitleFromFolderSplitsWords(string folder, string expected)
        {
            Assert.Equal(expected, InstallerHubParser.TitleFromFolder(folder));
        }

        [Fact]
        public void ParseReadmeTitlesReadsTheListedTitles()
        {
            var titles = InstallerHubParser.ParseReadmeTitles(Fixture.Read("hub-README.md"));
            // The README heading says 348, but the list itself has 351 bullets.
            Assert.Equal(351, titles.Count);
            Assert.Equal("7 Days to Die VR (motion controls)", titles[0]);
            Assert.Contains("Alan Wake", titles);
        }

        [Fact]
        public void AddReadmeTitlesSkipsTitlesAlreadyCoveredByScripts()
        {
            var entries = new List<VrModEntry>
            {
                InstallerHubParser.ParseScript("AlienIsolationVR", Fixture.Read("AlienIsolationVR-core.ps1"))
            };
            InstallerHubParser.AddReadmeTitles(entries, Fixture.Read("hub-README.md"));

            Assert.Single(entries, e => e.Title.StartsWith("Alien: Isolation", StringComparison.Ordinal));
            var alanWake = entries.Single(e => e.Title == "Alan Wake");
            Assert.Equal(InstallerHubSource.RepoUrl, alanWake.Url);
            Assert.Empty(alanWake.SteamAppIds);
        }
    }
}
