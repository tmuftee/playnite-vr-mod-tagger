using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VrModTagger.Models;
using VrModTagger.Sources;

namespace VrModTagger.Caching
{
    /// <summary>A source's entries as saved after its last successful fetch.</summary>
    public class CachedSource
    {
        /// <summary>Bump when parsing changes, so lists saved by an older plugin version are fetched again.</summary>
        public const int CurrentFormat = 1;

        public int Format { get; set; } = CurrentFormat;
        public string CacheKey { get; set; }
        public DateTime FetchedAt { get; set; }
        public List<VrModEntry> Entries { get; set; } = new List<VrModEntry>();
    }

    public interface ISourceCacheStore
    {
        /// <summary>Returns null when nothing is saved or the saved file can't be read.</summary>
        CachedSource Load(string cacheKey);

        void Save(CachedSource cached);
    }

    public enum SourceLoadKind
    {
        /// <summary>Saved list was recent enough; no network request.</summary>
        Cached,
        Fetched,

        /// <summary>Fetching failed, so an older saved list was used instead.</summary>
        StaleCache,

        /// <summary>Fetching failed and nothing usable was saved.</summary>
        Failed
    }

    public class SourceLoadResult
    {
        public string Source { get; set; }
        public SourceLoadKind Kind { get; set; }
        public List<VrModEntry> Entries { get; set; } = new List<VrModEntry>();
        public DateTime? FetchedAt { get; set; }
        public string Error { get; set; }
    }

    /// <summary>Gets each source's entries from the saved copy while it's recent, and from the site otherwise.</summary>
    public class SourceLoader
    {
        private readonly ISourceCacheStore store;
        private readonly TimeSpan maxAge;
        private readonly Func<DateTime> now;

        public SourceLoader(ISourceCacheStore store, TimeSpan maxAge, Func<DateTime> now = null)
        {
            this.store = store;
            this.maxAge = maxAge;
            this.now = now ?? (() => DateTime.Now);
        }

        /// <param name="source">The source to load.</param>
        /// <param name="forceRefresh">Fetch even if the saved list is recent.</param>
        /// <param name="offlineOnly">Never fetch; use the saved list only if it's recent (for automatic runs).</param>
        /// <param name="cancellationToken">Cancels the fetch.</param>
        public async Task<SourceLoadResult> LoadAsync(IVrModSource source, bool forceRefresh, bool offlineOnly, CancellationToken cancellationToken)
        {
            var cached = store.Load(source.CacheKey);
            if (cached != null && cached.Format != CachedSource.CurrentFormat)
            {
                cached = null;
            }

            if (cached != null && !forceRefresh && IsFresh(cached))
            {
                return Result(source, SourceLoadKind.Cached, cached);
            }

            if (offlineOnly)
            {
                return new SourceLoadResult { Source = source.Name, Kind = SourceLoadKind.Failed, Error = "No recent saved list." };
            }

            try
            {
                var entries = await source.FetchAsync(cancellationToken).ConfigureAwait(false);
                var fresh = new CachedSource { CacheKey = source.CacheKey, FetchedAt = now(), Entries = entries.ToList() };
                store.Save(fresh);
                return Result(source, SourceLoadKind.Fetched, fresh);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                if (cached != null)
                {
                    var stale = Result(source, SourceLoadKind.StaleCache, cached);
                    stale.Error = e.Message;
                    return stale;
                }

                return new SourceLoadResult { Source = source.Name, Kind = SourceLoadKind.Failed, Error = e.Message };
            }
        }

        public bool IsFresh(CachedSource cached) => now() - cached.FetchedAt < maxAge;

        private static SourceLoadResult Result(IVrModSource source, SourceLoadKind kind, CachedSource cached) => new SourceLoadResult
        {
            Source = source.Name,
            Kind = kind,
            Entries = cached.Entries ?? new List<VrModEntry>(),
            FetchedAt = cached.FetchedAt
        };
    }
}
