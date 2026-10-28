using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VrModTagger.Applying;
using VrModTagger.Matching;
using VrModTagger.Models;
using VrModTagger.Sources;
using Xunit;

namespace VrModTagger.Tests
{
    public class SecurityTests
    {
        [Theory]
        [InlineData("https://pcvrgamer.net/dead-space-2/")]
        [InlineData("https://www.pcvrgamer.net/dead-space-2/")]
        [InlineData("https://pcvrcentral.com/games/cyberpunk-2077")]
        [InlineData("https://mrsurvivor-installers.com/#vr-hub")]
        [InlineData("https://github.com/Mr-Nlce/PCVR-Mods-Installer-Hub/tree/HEAD/Core/AlienIsolationVR")]
        public void AllowsHttpsLinksToSources(string url)
        {
            Assert.True(LinkSafety.IsAllowed(url));
        }

        [Theory]
        [InlineData("file:///C:/Windows/System32/calc.exe")]
        [InlineData("C:\\Windows\\System32\\calc.exe")]
        [InlineData("\\\\server\\share\\x.exe")]
        [InlineData("javascript:alert(1)")]
        [InlineData("steam://run/123")]
        [InlineData("ms-settings:")]
        [InlineData("http://pcvrgamer.net/dead-space-2/")]
        [InlineData("https://evil.example/pcvrgamer.net")]
        [InlineData("https://pcvrgamer.net.evil.example/")]
        [InlineData("https://evilpcvrgamer.net/")]
        [InlineData("https://pcvrgamer.net@evil.example/")]
        [InlineData("https://user:pass@pcvrgamer.net/")]
        [InlineData("https://pcvrgamer.net:8443/")]
        [InlineData("https://raw.githubusercontent.com/x")]
        [InlineData("https://pcvrgamer.net/\nhttps://evil.example")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("not a url")]
        public void RejectsEverythingElse(string url)
        {
            Assert.False(LinkSafety.IsAllowed(url));
        }

        [Fact]
        public void UnsafeLinkFromSourceIsNeverAdded()
        {
            var game = new GameState { Id = Guid.NewGuid(), Name = "Game" };
            var entry = new VrModEntry { Source = "PCVR Gamer", Title = "Game", Url = "file:///C:/Windows/System32/calc.exe" };
            var match = new GameMatch { Game = new LibraryGame { Id = game.Id, Name = "Game" }, Entry = entry, Kind = MatchKind.Title };

            var change = Assert.Single(TagPlanner.Plan(new[] { game }, new[] { match }, new TaggingOptions()));
            Assert.Equal(new[] { "VR Mod" }, change.AddTags);
            Assert.Empty(change.AddLinks);
        }

        [Fact]
        public void UnsafeLinkAlreadyAddedIsRemovedEvenWithRemovalOff()
        {
            var bad = new LinkInfo { Name = "VR Mod (PCVR Gamer)", Url = "ms-settings:" };
            var game = new GameState { Id = Guid.NewGuid(), Name = "Game", TagNames = { "VR Mod" }, Links = { bad } };
            var entry = new VrModEntry { Source = "PCVR Gamer", Title = "Game", Url = "ms-settings:" };
            var match = new GameMatch { Game = new LibraryGame { Id = game.Id, Name = "Game" }, Entry = entry, Kind = MatchKind.Title };

            var change = Assert.Single(TagPlanner.Plan(new[] { game }, new[] { match }, new TaggingOptions { RemoveStaleTags = false }, new[] { "PCVR Gamer" }));
            Assert.Same(bad, Assert.Single(change.RemoveLinks));
        }

        [Fact]
        public void UsersOwnLinksAreNeverRemoved()
        {
            var mine = new LinkInfo { Name = "My notes", Url = "file:///C:/Games/notes.txt" };
            var game = new GameState { Id = Guid.NewGuid(), Name = "Game", Links = { mine } };
            Assert.Empty(TagPlanner.Plan(new[] { game }, new List<GameMatch>(), new TaggingOptions()));
        }

        [Fact]
        public void PcvrGamerPostWithHostileLinkIsNotLinked()
        {
            const string json = "[{\"id\":1,\"slug\":\"x\",\"link\":\"file:///C:/x.exe\",\"title\":{\"rendered\":\"Dead Space 2\"},\"content\":{\"rendered\":\"\"},\"categories\":[26]}]";
            var entry = PcvrGamerParser.ToEntry(PcvrGamerParser.ParsePosts(json).Single());
            Assert.False(LinkSafety.IsAllowed(entry.Url));
        }

        [Theory]
        [InlineData("https://raw.githubusercontent.com/Mr-Nlce/PCVR-Mods-Installer-Hub/HEAD/README.md")]
        [InlineData("https://pcvrgamer.net/wp-json/wp/v2/posts")]
        [InlineData("http://api.github.com/repos/x")]
        public async Task TokenIsOnlySentToGitHubApi(string url)
        {
            var headers = new Dictionary<string, string> { ["Authorization"] = "Bearer secret" };
            await Assert.ThrowsAsync<InvalidOperationException>(() => SourceHttp.GetStringAsync(url, CancellationToken.None, headers));
        }

        [Fact]
        public void HostilePageCannotHangParser()
        {
            // Many unclosed anchors would make a naive regex very slow; a timeout or a fast result are both fine,
            // as long as it doesn't run unbounded.
            var hostile = string.Concat(Enumerable.Repeat("<a href=\"https://store.steampowered.com/app/1/\">", 20000));
            var started = DateTime.UtcNow;
            try
            {
                PcvrGamerParser.FindGameSteamLinks("Game", hostile);
            }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
            {
            }

            Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(30));
        }

        [Fact]
        public void PcvrCentralDataCannotCreateTypes()
        {
            // JavaScriptSerializer can't create types without a resolver, and "__type" must not break parsing either.
            const string html = "<script>self.__next_f.push([1,\"5:{\\\"games\\\":[{\\\"__type\\\":\\\"System.IO.FileInfo, mscorlib\\\",\\\"slug\\\":\\\"x\\\",\\\"title\\\":\\\"X\\\",\\\"mods\\\":[]}]}\"])</script>";
            var game = Assert.Single(PcvrCentralParser.ParseGames(html));
            Assert.Equal("x", game.Slug);
        }
    }
}
