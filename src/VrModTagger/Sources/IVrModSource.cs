using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VrModTagger.Models;

namespace VrModTagger.Sources
{
    public interface IVrModSource
    {
        /// <summary>Display name, also used in link names ("VR Mod (Name)") and the cache key.</summary>
        string Name { get; }

        /// <summary>Name of the saved copy. Differs from <see cref="Name"/> when options change what is fetched.</summary>
        string CacheKey { get; }

        Task<IReadOnlyList<VrModEntry>> FetchAsync(CancellationToken cancellationToken);
    }
}
