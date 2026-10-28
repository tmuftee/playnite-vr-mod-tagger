using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VrModTagger.Sources;
using Xunit;
using Xunit.Abstractions;

namespace VrModTagger.Tests
{
    /// <summary>
    /// Fetches the real sites. They only run when VRMOD_LIVE_TESTS=1, so normal test runs stay offline.
    /// </summary>
    public class LiveSourceTests
    {
        private readonly ITestOutputHelper output;
        private static bool Enabled => Environment.GetEnvironmentVariable("VRMOD_LIVE_TESTS") == "1";

        public LiveSourceTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task MrSurvivorLive()
        {
            if (!Enabled)
            {
                return;
            }

            var entries = await new MrSurvivorSource().FetchAsync(CancellationToken.None);
            output.WriteLine($"{entries.Count} entries, {entries.Count(e => e.QuestOnly)} Quest-only");
            Assert.True(entries.Count > 30);
        }

        [Fact]
        public async Task PcvrCentralLive()
        {
            if (!Enabled)
            {
                return;
            }

            var entries = await new PcvrCentralSource().FetchAsync(CancellationToken.None);
            output.WriteLine($"{entries.Count} entries, {entries.Count(e => e.ModNames.Count > 0)} with mods, " +
                $"{entries.Count(e => e.NativeVr)} native VR, {entries.Count(e => e.QuestOnly)} Quest-only, " +
                $"{entries.Count(e => e.SteamAppIds.Count > 0)} with Steam AppIDs");
            Assert.True(entries.Count > 1000);
        }

        [Fact]
        public async Task PcvrGamerLive()
        {
            if (!Enabled)
            {
                return;
            }

            var entries = await new PcvrGamerSource().FetchAsync(CancellationToken.None);
            output.WriteLine($"{entries.Count} entries, {entries.Count(e => e.SteamAppIds.Count > 0)} with Steam AppIDs");
            foreach (var entry in entries.Where(e => e.SteamAppIds.Count > 0))
            {
                output.WriteLine($"  {entry.Title} = {string.Join(",", entry.SteamAppIds)} [{string.Join(" / ", entry.AlternateTitles)}]");
            }
            Assert.True(entries.Count > 50);
        }

        [Fact]
        public async Task InstallerHubLive()
        {
            if (!Enabled)
            {
                return;
            }

            var entries = await new InstallerHubSource(Environment.GetEnvironmentVariable("GITHUB_TOKEN"))
                .FetchAsync(CancellationToken.None);
            output.WriteLine($"{entries.Count} entries, {entries.Count(e => e.SteamAppIds.Count > 0)} with Steam AppIDs");
            Assert.True(entries.Count > 250);
        }
    }
}
