using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VrModTagger.Applying;
using VrModTagger.Caching;
using VrModTagger.Matching;
using VrModTagger.Models;
using VrModTagger.Sources;
using VrModTagger.UI;

namespace VrModTagger
{
    public class VrModTagger : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private const string MenuSection = "@VR Mod Tagger";
        private const string GameMenuSection = "VR Mod Tagger";
        private const string Title = "VR Mod Tagger";
        private const string NotificationId = "VrModTagger-run";

        private VrModTaggerSettingsViewModel settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("00b02d59-c671-4f56-bab9-46bcd8576ef2");

        public VrModTagger(IPlayniteAPI api) : base(api)
        {
            settings = new VrModTaggerSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        private enum RunMode
        {
            /// <summary>From a menu: may fetch, asks before changing anything.</summary>
            Interactive,

            /// <summary>After a library update: saved lists only, no questions, reports through a notification.</summary>
            Automatic
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                MenuSection = MenuSection,
                Description = "Update VR mod tags (all games)",
                Action = _ => Run(RunMode.Interactive, forceRefresh: false)
            };
            yield return new MainMenuItem
            {
                MenuSection = MenuSection,
                Description = "Refresh source data and update tags",
                Action = _ => Run(RunMode.Interactive, forceRefresh: true)
            };
            yield return new MainMenuItem
            {
                MenuSection = MenuSection,
                Description = "Review similar-title matches...",
                Action = _ => Run(RunMode.Interactive, forceRefresh: false, reviewAll: true)
            };
            yield return new MainMenuItem
            {
                MenuSection = MenuSection,
                Description = "Undo last run",
                Action = _ => UndoLastRun()
            };
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var games = args.Games.ToList();
            yield return new GameMenuItem
            {
                MenuSection = GameMenuSection,
                Description = games.Count == 1 ? "Check for VR mods" : $"Check {games.Count} games for VR mods",
                Action = _ => Run(RunMode.Interactive, forceRefresh: false, onlyGameIds: games.Select(g => g.Id).ToList())
            };

            var excluded = new HashSet<Guid>(settings.Settings.ExcludedGameIds);
            if (games.Any(g => !excluded.Contains(g.Id)))
            {
                yield return new GameMenuItem
                {
                    MenuSection = GameMenuSection,
                    Description = "Exclude from VR mod tagging",
                    Action = _ => Exclude(games.Where(g => !excluded.Contains(g.Id)).ToList())
                };
            }

            if (games.Any(g => excluded.Contains(g.Id)))
            {
                yield return new GameMenuItem
                {
                    MenuSection = GameMenuSection,
                    Description = "Include in VR mod tagging again",
                    Action = _ => Include(games.Where(g => excluded.Contains(g.Id)).ToList())
                };
            }
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            logger.Info("VR Mod Tagger loaded.");
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            if (settings.Settings.AutoUpdateAfterLibraryUpdate)
            {
                Run(RunMode.Automatic, forceRefresh: false);
            }
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new VrModTaggerSettingsView();
        }

        private IEnumerable<IVrModSource> GetEnabledSources()
        {
            var options = settings.Settings;
            if (options.EnablePcvrCentral)
            {
                yield return new PcvrCentralSource(options.IncludeNativeVr);
            }
            if (options.EnablePcvrGamer)
            {
                yield return new PcvrGamerSource();
            }
            if (options.EnableMrSurvivor)
            {
                yield return new MrSurvivorSource();
            }
            if (options.EnableInstallerHub)
            {
                yield return new InstallerHubSource(options.GitHubToken);
            }
        }

        /// <param name="mode">Interactive (menus) or automatic (after a library update).</param>
        /// <param name="forceRefresh">Fetch every source even if its saved list is recent.</param>
        /// <param name="onlyGameIds">Limit the run to these games; null for the whole library.</param>
        /// <param name="reviewAll">Show every similar-title match for review, including ones already answered.</param>
        private void Run(RunMode mode, bool forceRefresh, List<Guid> onlyGameIds = null, bool reviewAll = false)
        {
            var interactive = mode == RunMode.Interactive;
            var sources = GetEnabledSources().ToList();
            if (sources.Count == 0)
            {
                if (interactive)
                {
                    PlayniteApi.Dialogs.ShowMessage("No VR mod sources are enabled.", Title);
                }
                return;
            }

            var options = settings.Settings;
            var loader = new SourceLoader(new FileSourceCacheStore(GetPluginUserDataPath()), TimeSpan.FromDays(Math.Max(0, options.CacheDays)));
            var tagger = new LibraryTagger(PlayniteApi);
            var loadResults = new List<SourceLoadResult>();
            var matches = new List<GameMatch>();
            var gameStates = new List<GameState>();
            var cancelled = false;

            var progress = new GlobalProgressOptions("VR Mod Tagger: loading VR mod lists...", interactive) { IsIndeterminate = false };
            PlayniteApi.Dialogs.ActivateGlobalProgress(args =>
            {
                args.ProgressMaxValue = sources.Count + 1;
                try
                {
                    foreach (var source in sources)
                    {
                        args.Text = $"VR Mod Tagger: loading {source.Name}...";
                        var result = loader.LoadAsync(source, forceRefresh, offlineOnly: !interactive, args.CancelToken).GetAwaiter().GetResult();
                        if (result.Error != null)
                        {
                            logger.Warn($"{source.Name}: {result.Kind}, {result.Error}");
                        }
                        loadResults.Add(result);
                        args.CurrentProgressValue++;
                    }
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    return;
                }

                args.Text = "VR Mod Tagger: matching your library...";
                var entries = loadResults.SelectMany(r => r.Entries).ToList();
                var onlyIds = onlyGameIds == null ? null : new HashSet<Guid>(onlyGameIds);
                var games = PlayniteApi.Database.Games.Where(g => onlyIds == null || onlyIds.Contains(g.Id)).ToList();
                var matcher = new GameMatcher(entries, options.EnableFuzzyMatching, options.FuzzyThreshold);
                matches = matcher.Match(games.Select(LibraryGameFactory.FromPlaynite));
                gameStates = tagger.ReadGames().Where(g => onlyIds == null || onlyIds.Contains(g.Id)).ToList();
                args.CurrentProgressValue++;
            }, progress);

            if (cancelled)
            {
                return;
            }

            var failed = loadResults.Where(r => r.Kind == SourceLoadKind.Failed).Select(r => r.Source).ToList();
            if (failed.Count == sources.Count)
            {
                if (interactive)
                {
                    PlayniteApi.Dialogs.ShowErrorMessage("Couldn't load any VR mod list, so nothing was changed.\n\n" + DescribeSources(loadResults), Title);
                }
                else
                {
                    logger.Info("Automatic run skipped: no recent saved lists. Run an update from the menu first.");
                }
                return;
            }

            // An automatic run must not remove tags because a list is merely out of date.
            if (!interactive && failed.Count > 0)
            {
                logger.Info($"Automatic run skipped: no recent saved list for {string.Join(", ", failed)}.");
                return;
            }

            // Similar-title matches: apply earlier answers, then ask about new ones.
            var undecided = FuzzyReview.ApplyDecisions(matches, options.FuzzyDecisions);
            var toReview = reviewAll ? matches.Where(m => m.Kind == MatchKind.Fuzzy).ToList() : undecided;
            if (interactive && reviewAll && toReview.Count == 0)
            {
                PlayniteApi.Dialogs.ShowMessage(options.EnableFuzzyMatching
                    ? "There are no similar-title matches to review."
                    : "Similar-title matching is off. Turn it on in the add-on's settings to get suggestions.", Title);
                return;
            }

            if (interactive && toReview.Count > 0 && ReviewFuzzyMatches(toReview))
            {
                options.FuzzyDecisions = FuzzyReview.Record(options.FuzzyDecisions, toReview);
                SavePluginSettings(options);
            }

            var changes = TagPlanner.Plan(gameStates, matches, ToTaggingOptions(options), failed);
            logger.Info($"Run ({mode}, forceRefresh={forceRefresh}, games={gameStates.Count}): {matches.Count} matches, {changes.Count} games to change.");

            if (!interactive)
            {
                ApplyAndReport(tagger, changes);
                return;
            }

            var report = onlyGameIds != null && onlyGameIds.Count <= 10
                ? DescribeGames(gameStates, matches) + "\n\n" + DescribeSources(loadResults)
                : DescribeSources(loadResults) + "\n\n" + DescribeMatches(matches);
            if (failed.Count > 0)
            {
                report += "\n\nSome lists couldn't be loaded, so no tags are removed this time and links from those lists are kept.";
            }

            // Non-zero only if the review was cancelled.
            var pendingReview = FuzzyReview.ApplyDecisions(matches, options.FuzzyDecisions).Count;
            if (pendingReview > 0)
            {
                report += $"\n\n{pendingReview} similar-title matches are waiting for review (VR Mod Tagger menu > Review similar-title matches).";
            }

            if (changes.Count == 0)
            {
                PlayniteApi.Dialogs.ShowMessage(report + "\n\nNothing needs to change.", Title);
                return;
            }

            var answer = PlayniteApi.Dialogs.ShowMessage(
                report + "\n\nChanges:\n" + DescribeChanges(changes, options) + "\n\nApply these changes?",
                Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                ApplyAndReport(tagger, changes);
            }
        }

        private void ApplyAndReport(LibraryTagger tagger, List<GameChange> changes)
        {
            if (changes.Count == 0)
            {
                return;
            }

            var applied = tagger.Apply(changes);
            new RunLog { RunAt = DateTime.Now, Changes = applied }.Save(GetPluginUserDataPath());
            logger.Info($"Applied changes to {applied.Count} games.");

            var options = settings.Settings;
            var tagged = applied.Count(c => c.AddTags.Contains(options.VrModTagName, StringComparer.OrdinalIgnoreCase));
            var native = applied.Count(c => c.AddTags.Contains(options.NativeVrTagName, StringComparer.OrdinalIgnoreCase));
            var removed = applied.Count(c => c.RemoveTags.Count > 0);
            var parts = new List<string>();
            if (tagged > 0) parts.Add($"{tagged} newly tagged \"{options.VrModTagName}\"");
            if (native > 0) parts.Add($"{native} newly tagged \"{options.NativeVrTagName}\"");
            if (removed > 0) parts.Add($"{removed} had old tags removed");
            var summary = parts.Count > 0 ? string.Join(", ", parts) : "links updated";

            PlayniteApi.Notifications.Add(new NotificationMessage(
                NotificationId,
                $"VR Mod Tagger updated {applied.Count} games ({summary}). Undo it from Extensions > VR Mod Tagger.",
                NotificationType.Info));
        }

        private bool ReviewFuzzyMatches(List<GameMatch> toReview)
        {
            var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions { ShowMinimizeButton = false, ShowMaximizeButton = true, ShowCloseButton = true });
            var view = new FuzzyReviewView(toReview, window);
            window.Title = $"VR Mod Tagger: review {toReview.Count} similar-title matches";
            window.Width = 900;
            window.Height = 600;
            window.Content = view;
            window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();

            if (!view.Saved)
            {
                return false;
            }

            foreach (var row in view.Rows)
            {
                row.Match.Approved = row.Accept;
            }

            return true;
        }

        private void Exclude(List<Game> games)
        {
            var answer = PlayniteApi.Dialogs.ShowMessage(
                $"Stop tagging {Describe(games)} and remove the VR tags and links this add-on added?",
                Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            var options = settings.Settings;
            options.ExcludedGameIds = options.ExcludedGameIds.Union(games.Select(g => g.Id)).ToList();
            SavePluginSettings(options);

            var ids = new HashSet<Guid>(games.Select(g => g.Id));
            var tagger = new LibraryTagger(PlayniteApi);
            var changes = TagPlanner.Plan(tagger.ReadGames().Where(g => ids.Contains(g.Id)), new List<GameMatch>(), ToTaggingOptions(options));
            if (changes.Count == 0)
            {
                PlayniteApi.Dialogs.ShowMessage($"{Describe(games)} won't be tagged from now on. There were no VR tags or links from this add-on to remove.", Title);
                return;
            }

            ApplyAndReport(tagger, changes);
        }

        private void Include(List<Game> games)
        {
            var options = settings.Settings;
            var ids = new HashSet<Guid>(games.Select(g => g.Id));
            options.ExcludedGameIds = options.ExcludedGameIds.Where(id => !ids.Contains(id)).ToList();
            SavePluginSettings(options);

            var answer = PlayniteApi.Dialogs.ShowMessage(
                $"{Describe(games)} will be tagged again. Check for VR mods now?",
                Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                Run(RunMode.Interactive, forceRefresh: false, onlyGameIds: ids.ToList());
            }
        }

        private void UndoLastRun()
        {
            var dataPath = GetPluginUserDataPath();
            RunLog log;
            try
            {
                log = RunLog.Load(dataPath);
            }
            catch (Exception e)
            {
                logger.Error(e, "Reading the last run's changes failed.");
                PlayniteApi.Dialogs.ShowErrorMessage("The record of the last run couldn't be read, so it can't be undone.", Title);
                return;
            }

            if (log == null || log.Changes.Count == 0)
            {
                PlayniteApi.Dialogs.ShowMessage("There's no run to undo.", Title);
                return;
            }

            var answer = PlayniteApi.Dialogs.ShowMessage(
                $"Undo the changes made on {log.RunAt:g} to {log.Changes.Count} games?\n\n" +
                "Only the tags and links that run added or removed are changed back.",
                Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            var undone = new LibraryTagger(PlayniteApi).Apply(log.UndoChanges());
            RunLog.Delete(dataPath);
            logger.Info($"Undid changes on {undone.Count} games.");
            PlayniteApi.Dialogs.ShowMessage($"Undid the changes on {undone.Count} games.", Title);
        }

        private static TaggingOptions ToTaggingOptions(VrModTaggerSettings options)
        {
            return new TaggingOptions
            {
                VrModTagName = options.VrModTagName.Trim(),
                IncludeNativeVr = options.IncludeNativeVr,
                NativeVrTagName = options.NativeVrTagName.Trim(),
                AddQuestTag = options.AddQuestTag,
                QuestTagName = options.QuestTagName.Trim(),
                AddPerSourceTags = options.AddPerSourceTags,
                RemoveStaleTags = options.RemoveStaleTags,
                ExcludedGameIds = new HashSet<Guid>(options.ExcludedGameIds ?? new List<Guid>())
            };
        }

        private static string Describe(List<Game> games) => games.Count == 1 ? $"\"{games[0].Name}\"" : $"{games.Count} games";

        private static string DescribeSources(List<SourceLoadResult> results)
        {
            string Line(SourceLoadResult r)
            {
                var count = $"{r.Entries.Count} games";
                switch (r.Kind)
                {
                    case SourceLoadKind.Cached: return $"{r.Source}: {count} (saved {r.FetchedAt:g})";
                    case SourceLoadKind.Fetched: return $"{r.Source}: {count} (just fetched)";
                    case SourceLoadKind.StaleCache: return $"{r.Source}: couldn't fetch ({r.Error}); using the list saved {r.FetchedAt:g}";
                    default: return $"{r.Source}: couldn't load ({r.Error})";
                }
            }

            return "VR mod lists:\n" + string.Join("\n", results.Select(Line));
        }

        private static string DescribeGames(List<GameState> games, List<GameMatch> matches)
        {
            var lines = games.OrderBy(g => g.Name).Select(game =>
            {
                var found = matches.Where(m => m.Game.Id == game.Id && m.IsApplied).Select(m => m.Entry).ToList();
                var modSources = found.Where(e => !TagPlanner.IsNativeOnly(e)).Select(e => e.Source).Distinct().ToList();
                var parts = new List<string>();
                if (modSources.Count > 0)
                {
                    parts.Add("VR mod listed on " + string.Join(", ", modSources));
                }
                if (found.Any(e => e.NativeVr))
                {
                    parts.Add("has built-in VR");
                }

                return $"• {game.Name}: {(parts.Count > 0 ? string.Join("; ", parts) : "no VR mod found")}";
            });
            return string.Join("\n", lines);
        }

        private static string DescribeMatches(List<GameMatch> matches)
        {
            var applied = matches.Where(m => m.IsApplied).ToList();
            var withMod = applied.Where(m => !TagPlanner.IsNativeOnly(m.Entry)).Select(m => m.Game.Id).Distinct().Count();
            var native = applied.Where(m => m.Entry.NativeVr).Select(m => m.Game.Id).Distinct().Count();
            var byKind = string.Join(", ", applied.GroupBy(m => m.Kind).OrderBy(g => g.Key).Select(g => $"{g.Count()} by {Describe(g.Key)}"));

            var text = $"Library games with a VR mod: {withMod}\nLibrary games with built-in VR: {native}";
            if (applied.Count > 0)
            {
                text += $"\nMatches: {byKind}";
            }

            return text;
        }

        private static string Describe(MatchKind kind)
        {
            switch (kind)
            {
                case MatchKind.SteamAppId: return "Steam ID";
                case MatchKind.SteamLink: return "Steam store link";
                case MatchKind.Title: return "title";
                default: return "approved similar title";
            }
        }

        private static string DescribeChanges(List<GameChange> changes, VrModTaggerSettings options)
        {
            int GamesAdding(string tag) => changes.Count(c => c.AddTags.Contains(tag, StringComparer.OrdinalIgnoreCase));
            int GamesRemoving(string tag) => changes.Count(c => c.RemoveTags.Contains(tag, StringComparer.OrdinalIgnoreCase));

            var lines = new List<string>();
            void Line(int count, string text)
            {
                if (count > 0)
                {
                    lines.Add($"• {count} {(count == 1 ? "game" : "games")} {text}");
                }
            }

            Line(GamesAdding(options.VrModTagName), $"get the \"{options.VrModTagName}\" tag");
            Line(GamesAdding(options.NativeVrTagName), $"get the \"{options.NativeVrTagName}\" tag");
            Line(GamesAdding(options.QuestTagName), $"get the \"{options.QuestTagName}\" tag");
            Line(GamesRemoving(options.VrModTagName), $"lose the \"{options.VrModTagName}\" tag");
            Line(GamesRemoving(options.NativeVrTagName), $"lose the \"{options.NativeVrTagName}\" tag");
            Line(GamesRemoving(options.QuestTagName), $"lose the \"{options.QuestTagName}\" tag");
            Line(changes.Count(c => c.AddLinks.Count > 0), $"get new links ({changes.Sum(c => c.AddLinks.Count)} in all)");
            Line(changes.Count(c => c.RemoveLinks.Count > 0), $"lose old links ({changes.Sum(c => c.RemoveLinks.Count)} in all)");

            var otherTags = changes.Sum(c => c.AddTags.Count + c.RemoveTags.Count)
                - new[] { options.VrModTagName, options.NativeVrTagName, options.QuestTagName }.Distinct().Sum(t => GamesAdding(t) + GamesRemoving(t));
            if (otherTags > 0)
            {
                lines.Add($"• {otherTags} per-source tag changes");
            }

            return string.Join("\n", lines);
        }
    }
}
