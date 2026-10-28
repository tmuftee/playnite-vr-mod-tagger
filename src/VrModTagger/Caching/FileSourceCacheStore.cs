using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.IO;
using System.Linq;

namespace VrModTagger.Caching
{
    /// <summary>Saves each source's list as JSON in the plugin's data folder.</summary>
    public class FileSourceCacheStore : ISourceCacheStore
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly string folder;

        public FileSourceCacheStore(string dataPath)
        {
            folder = Path.Combine(dataPath, "cache");
        }

        public CachedSource Load(string cacheKey)
        {
            var path = PathFor(cacheKey);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return Serialization.FromJsonFile<CachedSource>(path);
            }
            catch (Exception e)
            {
                logger.Warn(e, $"Saved list for {cacheKey} couldn't be read; it will be fetched again.");
                return null;
            }
        }

        public void Save(CachedSource cached)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(PathFor(cached.CacheKey), Serialization.ToJson(cached));
        }

        private string PathFor(string cacheKey)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var name = new string(cacheKey.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return Path.Combine(folder, name + ".json");
        }
    }
}
