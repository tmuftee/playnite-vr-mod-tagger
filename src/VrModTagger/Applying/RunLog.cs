using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VrModTagger.Applying
{
    /// <summary>
    /// The changes made by the last run, saved so they can be undone. Undo reverses exactly these changes,
    /// so edits made by hand since then are left alone.
    /// </summary>
    public class RunLog
    {
        public const string FileName = "last-run.json";

        public DateTime RunAt { get; set; }
        public List<GameChange> Changes { get; set; } = new List<GameChange>();

        public List<GameChange> UndoChanges() => Changes.Select(c => c.Inverted()).ToList();

        public static string PathIn(string dataPath) => Path.Combine(dataPath, FileName);

        public void Save(string dataPath)
        {
            Directory.CreateDirectory(dataPath);
            File.WriteAllText(PathIn(dataPath), Serialization.ToJson(this, true));
        }

        public static RunLog Load(string dataPath)
        {
            var path = PathIn(dataPath);
            return File.Exists(path) ? Serialization.FromJsonFile<RunLog>(path) : null;
        }

        public static void Delete(string dataPath)
        {
            var path = PathIn(dataPath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
