using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.IO;

namespace VrModTagger.Applying
{
    /// <summary>Saves the tagging history as JSON in the add-on's data folder.</summary>
    public class HistoryStore
    {
        public const string FileName = "history.json";

        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly string path;

        public HistoryStore(string dataPath)
        {
            path = Path.Combine(dataPath, FileName);
        }

        public TagHistoryData Load()
        {
            if (!File.Exists(path))
            {
                return new TagHistoryData();
            }

            try
            {
                return Serialization.FromJsonFile<TagHistoryData>(path) ?? new TagHistoryData();
            }
            catch (Exception e)
            {
                // Keep the unreadable file for inspection rather than overwriting it on the next save.
                logger.Error(e, "Tagging history couldn't be read; starting a new one.");
                try
                {
                    File.Copy(path, path + ".broken", overwrite: true);
                }
                catch (Exception copyError)
                {
                    logger.Warn(copyError, "Couldn't keep a copy of the unreadable history.");
                }

                return new TagHistoryData();
            }
        }

        public void Save(TagHistoryData history)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".tmp";
            File.WriteAllText(temp, Serialization.ToJson(history));
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }
    }
}
