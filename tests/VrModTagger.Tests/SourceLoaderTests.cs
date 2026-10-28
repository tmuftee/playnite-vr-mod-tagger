using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using VrModTagger.Caching;
using VrModTagger.Models;
using VrModTagger.Sources;
using Xunit;

namespace VrModTagger.Tests
{
    public class SourceLoaderTests
    {
        private class MemoryStore : ISourceCacheStore
        {
            public Dictionary<string, CachedSource> Saved { get; } = new Dictionary<string, CachedSource>();
            public CachedSource Load(string cacheKey) => Saved.TryGetValue(cacheKey, out var c) ? c : null;
            public void Save(CachedSource cached) => Saved[cached.CacheKey] = cached;
        }

        private class FakeSource : IVrModSource
        {
            public string Name => "Fake";
            public string CacheKey => "Fake";
            public int Fetches { get; private set; }
            public bool Fail { get; set; }

            public Task<IReadOnlyList<VrModEntry>> FetchAsync(CancellationToken cancellationToken)
            {
                Fetches++;
                if (Fail)
                {
                    throw new HttpRequestException("site down");
                }

                IReadOnlyList<VrModEntry> entries = new List<VrModEntry> { new VrModEntry { Source = Name, Title = "Fresh" } };
                return Task.FromResult(entries);
            }
        }

        private static readonly DateTime now = new DateTime(2026, 10, 28, 12, 0, 0);

        private static CachedSource Saved(DateTime fetchedAt, int format = CachedSource.CurrentFormat) => new CachedSource
        {
            CacheKey = "Fake",
            FetchedAt = fetchedAt,
            Format = format,
            Entries = { new VrModEntry { Source = "Fake", Title = "Saved" } }
        };

        private static SourceLoader Loader(MemoryStore store) => new SourceLoader(store, TimeSpan.FromDays(7), () => now);

        [Fact]
        public async Task FetchesAndSavesWhenNothingIsSaved()
        {
            var store = new MemoryStore();
            var source = new FakeSource();
            var result = await Loader(store).LoadAsync(source, false, false, CancellationToken.None);

            Assert.Equal(SourceLoadKind.Fetched, result.Kind);
            Assert.Equal("Fresh", Assert.Single(result.Entries).Title);
            Assert.Equal(now, store.Saved["Fake"].FetchedAt);
        }

        [Fact]
        public async Task UsesRecentSavedListWithoutFetching()
        {
            var store = new MemoryStore();
            store.Save(Saved(now.AddDays(-2)));
            var source = new FakeSource();
            var result = await Loader(store).LoadAsync(source, false, false, CancellationToken.None);

            Assert.Equal(SourceLoadKind.Cached, result.Kind);
            Assert.Equal("Saved", Assert.Single(result.Entries).Title);
            Assert.Equal(0, source.Fetches);
        }

        [Fact]
        public async Task RefetchesOldSavedList()
        {
            var store = new MemoryStore();
            store.Save(Saved(now.AddDays(-8)));
            var result = await Loader(store).LoadAsync(new FakeSource(), false, false, CancellationToken.None);
            Assert.Equal(SourceLoadKind.Fetched, result.Kind);
        }

        [Fact]
        public async Task ForceRefreshIgnoresRecentSavedList()
        {
            var store = new MemoryStore();
            store.Save(Saved(now.AddHours(-1)));
            var source = new FakeSource();
            var result = await Loader(store).LoadAsync(source, true, false, CancellationToken.None);
            Assert.Equal(SourceLoadKind.Fetched, result.Kind);
            Assert.Equal(1, source.Fetches);
        }

        [Fact]
        public async Task FallsBackToOldSavedListWhenFetchFails()
        {
            var store = new MemoryStore();
            store.Save(Saved(now.AddDays(-30)));
            var result = await Loader(store).LoadAsync(new FakeSource { Fail = true }, false, false, CancellationToken.None);

            Assert.Equal(SourceLoadKind.StaleCache, result.Kind);
            Assert.Equal("Saved", Assert.Single(result.Entries).Title);
            Assert.Equal("site down", result.Error);
        }

        [Fact]
        public async Task FailsWhenFetchFailsAndNothingIsSaved()
        {
            var result = await Loader(new MemoryStore()).LoadAsync(new FakeSource { Fail = true }, false, false, CancellationToken.None);
            Assert.Equal(SourceLoadKind.Failed, result.Kind);
            Assert.Empty(result.Entries);
        }

        [Fact]
        public async Task OfflineOnlyNeverFetches()
        {
            var store = new MemoryStore();
            store.Save(Saved(now.AddDays(-8)));
            var source = new FakeSource();

            var old = await Loader(store).LoadAsync(source, false, true, CancellationToken.None);
            Assert.Equal(SourceLoadKind.Failed, old.Kind);

            store.Save(Saved(now.AddDays(-1)));
            var recent = await Loader(store).LoadAsync(source, false, true, CancellationToken.None);
            Assert.Equal(SourceLoadKind.Cached, recent.Kind);
            Assert.Equal(0, source.Fetches);
        }

        [Fact]
        public async Task IgnoresListSavedByOlderPluginVersion()
        {
            var store = new MemoryStore();
            store.Save(Saved(now.AddHours(-1), format: CachedSource.CurrentFormat - 1));
            var source = new FakeSource();
            var result = await Loader(store).LoadAsync(source, false, false, CancellationToken.None);
            Assert.Equal(SourceLoadKind.Fetched, result.Kind);
        }

        [Fact]
        public async Task CancellationIsNotTreatedAsFailure()
        {
            var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            var source = new CancellingSource();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Loader(new MemoryStore()).LoadAsync(source, false, false, cancelled.Token));
        }

        private class CancellingSource : IVrModSource
        {
            public string Name => "Cancel";
            public string CacheKey => "Cancel";

            public Task<IReadOnlyList<VrModEntry>> FetchAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<VrModEntry>>(new List<VrModEntry>());
            }
        }
    }
}
