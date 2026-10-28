using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;

namespace VrModTagger
{
    public class VrModTaggerSettings : ObservableObject
    {
        private bool enablePcvrCentral = true;
        private bool enablePcvrGamer = true;
        private bool enableMrSurvivor = true;
        private bool enableInstallerHub = true;
        private string vrModTagName = "VR Mod";
        private bool includeNativeVr = true;
        private string nativeVrTagName = "Native VR";
        private bool addQuestTag = false;
        private string questTagName = "VR Mod: Meta Quest";
        private bool addPerSourceTags = false;
        private bool removeStaleTags = true;
        private bool enableFuzzyMatching = false;
        private double fuzzyThreshold = 0.95;
        private int cacheDays = 7;
        private string gitHubToken = string.Empty;

        public bool EnablePcvrCentral { get => enablePcvrCentral; set => SetValue(ref enablePcvrCentral, value); }
        public bool EnablePcvrGamer { get => enablePcvrGamer; set => SetValue(ref enablePcvrGamer, value); }
        public bool EnableMrSurvivor { get => enableMrSurvivor; set => SetValue(ref enableMrSurvivor, value); }
        public bool EnableInstallerHub { get => enableInstallerHub; set => SetValue(ref enableInstallerHub, value); }
        public string VrModTagName { get => vrModTagName; set => SetValue(ref vrModTagName, value); }
        public bool IncludeNativeVr { get => includeNativeVr; set => SetValue(ref includeNativeVr, value); }
        public string NativeVrTagName { get => nativeVrTagName; set => SetValue(ref nativeVrTagName, value); }
        public bool AddQuestTag { get => addQuestTag; set => SetValue(ref addQuestTag, value); }
        public string QuestTagName { get => questTagName; set => SetValue(ref questTagName, value); }
        public bool AddPerSourceTags { get => addPerSourceTags; set => SetValue(ref addPerSourceTags, value); }
        public bool RemoveStaleTags { get => removeStaleTags; set => SetValue(ref removeStaleTags, value); }
        public bool EnableFuzzyMatching { get => enableFuzzyMatching; set => SetValue(ref enableFuzzyMatching, value); }
        public double FuzzyThreshold { get => fuzzyThreshold; set => SetValue(ref fuzzyThreshold, value); }
        public int CacheDays { get => cacheDays; set => SetValue(ref cacheDays, value); }
        public string GitHubToken { get => gitHubToken; set => SetValue(ref gitHubToken, value); }

        private bool autoUpdateAfterLibraryUpdate = false;
        public bool AutoUpdateAfterLibraryUpdate { get => autoUpdateAfterLibraryUpdate; set => SetValue(ref autoUpdateAfterLibraryUpdate, value); }

        /// <summary>Games the plugin never tags; their VR tags and links are removed on the next run.</summary>
        public List<Guid> ExcludedGameIds { get; set; } = new List<Guid>();

        /// <summary>Answers given in the similar-title review, so suggestions aren't asked again.</summary>
        public List<Matching.FuzzyDecision> FuzzyDecisions { get; set; } = new List<Matching.FuzzyDecision>();

        [DontSerialize]
        public string ExcludedGamesText => ExcludedGameIds.Count == 0
            ? "No games are excluded. Right-click a game > VR Mod Tagger to exclude it."
            : $"{ExcludedGameIds.Count} game(s) excluded. Right-click a game > VR Mod Tagger to include it again.";
    }

    public class VrModTaggerSettingsViewModel : ObservableObject, ISettings
    {
        private readonly VrModTagger plugin;
        private VrModTaggerSettings editingClone { get; set; }

        private VrModTaggerSettings settings;
        public VrModTaggerSettings Settings
        {
            get => settings;
            set => SetValue(ref settings, value);
        }

        public VrModTaggerSettingsViewModel(VrModTagger plugin)
        {
            this.plugin = plugin;
            Settings = plugin.LoadPluginSettings<VrModTaggerSettings>() ?? new VrModTaggerSettings();
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Settings.VrModTagName))
            {
                errors.Add("VR Mod tag name can't be empty.");
            }
            if (Settings.IncludeNativeVr && string.IsNullOrWhiteSpace(Settings.NativeVrTagName))
            {
                errors.Add("Native VR tag name can't be empty.");
            }
            if (Settings.AddQuestTag && string.IsNullOrWhiteSpace(Settings.QuestTagName))
            {
                errors.Add("Meta Quest tag name can't be empty.");
            }
            if (Settings.FuzzyThreshold <= 0 || Settings.FuzzyThreshold > 1)
            {
                errors.Add("Fuzzy match threshold must be between 0 and 1.");
            }
            if (Settings.CacheDays < 0)
            {
                errors.Add("Cache days can't be negative.");
            }
            return errors.Count == 0;
        }
    }
}
