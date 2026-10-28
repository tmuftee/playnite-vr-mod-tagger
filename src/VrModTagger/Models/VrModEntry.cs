using System.Collections.Generic;

namespace VrModTagger.Models
{
    /// <summary>
    /// One game listed by a VR mod source.
    /// </summary>
    public class VrModEntry
    {
        public string Source { get; set; }
        public string Title { get; set; }

        /// <summary>Other names the source gives for the game (e.g. "Halo: The Master Chief Collection" for "Halo MCC VR").</summary>
        public List<string> AlternateTitles { get; set; } = new List<string>();

        /// <summary>Steam AppIDs of the game. Some mods support several editions, so there can be more than one.</summary>
        public List<uint> SteamAppIds { get; set; } = new List<uint>();

        /// <summary>Link added to the matched Playnite game.</summary>
        public string Url { get; set; }

        public List<string> ModNames { get; set; } = new List<string>();

        /// <summary>Platforms the mod supports (MrSurvivor only).</summary>
        public List<string> Platforms { get; set; } = new List<string>();

        /// <summary>The mod only exists for Meta Quest (MrSurvivor "Meta Quest" rows).</summary>
        public bool QuestOnly { get; set; }

        /// <summary>The game has built-in VR (PCVR Central native VR list).</summary>
        public bool NativeVr { get; set; }

        /// <summary>The source lists the mod but says it's temporarily unavailable.</summary>
        public bool TemporarilyUnavailable { get; set; }
    }
}
