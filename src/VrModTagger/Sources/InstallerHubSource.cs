using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VrModTagger.Matching;
using VrModTagger.Models;

namespace VrModTagger.Sources
{
    /// <summary>
    /// Reads the per-game installer scripts and the title list from the PCVR Mods Installer Hub GitHub repository.
    /// </summary>
    public class InstallerHubSource : IVrModSource
    {
        public const string SourceName = "PCVR Mods Installer Hub";
        public const string RepoUrl = "https://github.com/Mr-Nlce/PCVR-Mods-Installer-Hub";
        private const string TreeUrl = "https://api.github.com/repos/Mr-Nlce/PCVR-Mods-Installer-Hub/git/trees/HEAD?recursive=1";
        private const string RawBaseUrl = "https://raw.githubusercontent.com/Mr-Nlce/PCVR-Mods-Installer-Hub/HEAD/";

        private readonly string gitHubToken;

        public string Name => SourceName;
        public string CacheKey => SourceName;

        public InstallerHubSource(string gitHubToken = null)
        {
            this.gitHubToken = gitHubToken;
        }

        public async Task<IReadOnlyList<VrModEntry>> FetchAsync(CancellationToken cancellationToken)
        {
            var apiHeaders = new Dictionary<string, string> { ["Accept"] = "application/vnd.github+json" };
            if (!string.IsNullOrWhiteSpace(gitHubToken))
            {
                apiHeaders["Authorization"] = "Bearer " + gitHubToken.Trim();
            }

            var treeJson = await SourceHttp.GetStringAsync(TreeUrl, cancellationToken, apiHeaders).ConfigureAwait(false);
            var tree = InstallerHubParser.ParseTree(treeJson);

            // raw.githubusercontent.com doesn't count against the GitHub API rate limit.
            var entries = await SourceHttp.ForEachThrottledAsync(tree.Scripts, 8, async script =>
            {
                var text = await SourceHttp.GetStringAsync(RawBaseUrl + script.Path, cancellationToken).ConfigureAwait(false);
                return InstallerHubParser.ParseScript(script.Folder, text);
            }).ConfigureAwait(false);

            var readme = await SourceHttp.GetStringAsync(RawBaseUrl + "README.md", cancellationToken).ConfigureAwait(false);
            InstallerHubParser.AddReadmeTitles(entries, readme);
            return entries;
        }
    }

    public class InstallerHubTree
    {
        public string Sha { get; set; }
        public List<InstallerHubScript> Scripts { get; set; } = new List<InstallerHubScript>();
    }

    public class InstallerHubScript
    {
        /// <summary>Game folder under Core, e.g. "AlienIsolationVR".</summary>
        public string Folder { get; set; }

        /// <summary>Repository path, e.g. "Core/AlienIsolationVR/AlienIsolationVR-core.ps1".</summary>
        public string Path { get; set; }
    }

    public static class InstallerHubParser
    {
        // Steam AppIDs of tools rather than games: SteamVR and Steamworks Common Redistributables.
        private static readonly HashSet<uint> ignoredAppIds = new HashSet<uint> { 250820, 228980 };

        private static readonly Regex scriptPathRegex = new Regex(@"^Core/(?<folder>[^/]+)/\k<folder>-core\.ps1$", RegexOptions.IgnoreCase, SourceHttp.RegexTimeout);
        private static readonly Regex appIdLineRegex = new Regex(@"app_?id", RegexOptions.IgnoreCase, SourceHttp.RegexTimeout);
        private static readonly Regex quotedNumberRegex = new Regex(@"['""](?<id>\d{3,7})['""]", RegexOptions.None, SourceHttp.RegexTimeout);
        private static readonly Regex bareAppIdRegex = new Regex(@"app_?id\s*[:=]?\s*(?<id>\d{3,7})\b", RegexOptions.IgnoreCase, SourceHttp.RegexTimeout);
        private static readonly Regex appManifestRegex = new Regex(@"appmanifest_(?<id>\d{3,7})\b", RegexOptions.IgnoreCase, SourceHttp.RegexTimeout);

        // In order of preference.
        private static readonly Regex[] titleRegexes =
        {
            new Regex(@"WindowTitle\s*=\s*(?<q>['""])(?<title>.+?)\k<q>", RegexOptions.IgnoreCase, SourceHttp.RegexTimeout),
            new Regex(@"-GameTitle\s+(?<q>['""])(?<title>.+?)\k<q>", RegexOptions.IgnoreCase, SourceHttp.RegexTimeout),
            new Regex(@"-GameName\s+(?<q>['""])(?<title>.+?)\k<q>", RegexOptions.IgnoreCase, SourceHttp.RegexTimeout),
            new Regex(@"\$GAME_NAME\s*=\s*(?<q>['""])(?<title>.+?)\k<q>", RegexOptions.IgnoreCase, SourceHttp.RegexTimeout),
            new Regex(@"-Title\s+(?<q>['""])(?<title>.+?)\k<q>", RegexOptions.IgnoreCase, SourceHttp.RegexTimeout)
        };

        private static readonly Regex installerSuffixRegex = new Regex(@"\s+(Mod\s+)?Installer\s*$", RegexOptions.IgnoreCase, SourceHttp.RegexTimeout);
        private static readonly Regex steamFolderRegex = new Regex(@"-SteamFolderNames\s*@\((?<names>[^)]*)\)", RegexOptions.IgnoreCase, SourceHttp.RegexTimeout);
        private static readonly Regex quotedStringRegex = new Regex(@"(?<q>['""])(?<value>.+?)\k<q>", RegexOptions.None, SourceHttp.RegexTimeout);

        public static InstallerHubTree ParseTree(string json)
        {
            GitHubTreeResponse response;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                response = (GitHubTreeResponse)new DataContractJsonSerializer(typeof(GitHubTreeResponse)).ReadObject(stream);
            }

            if (response?.Tree == null)
            {
                throw new FormatException("GitHub tree response had no entries.");
            }

            if (response.Truncated)
            {
                throw new FormatException("GitHub tree response was truncated; the repository is too large to list in one call.");
            }

            var tree = new InstallerHubTree { Sha = response.Sha };
            foreach (var item in response.Tree.Where(t => t.Type == "blob" && t.Path != null))
            {
                var match = scriptPathRegex.Match(item.Path);
                if (match.Success)
                {
                    tree.Scripts.Add(new InstallerHubScript { Folder = match.Groups["folder"].Value, Path = item.Path });
                }
            }

            return tree;
        }

        public static VrModEntry ParseScript(string folder, string script)
        {
            // Scripts often name the game more than once (window title, game name, Steam folder); keep them all for matching.
            var titles = FindTitles(script);
            var entry = new VrModEntry
            {
                Source = InstallerHubSource.SourceName,
                Title = titles.FirstOrDefault() ?? TitleFromFolder(folder),
                Url = $"{InstallerHubSource.RepoUrl}/tree/HEAD/Core/{folder}",
                SteamAppIds = FindSteamAppIds(script)
            };

            foreach (var title in titles.Skip(1))
            {
                AddAlternateTitle(entry, title);
            }

            foreach (Match match in steamFolderRegex.Matches(script))
            {
                foreach (Match name in quotedStringRegex.Matches(match.Groups["names"].Value))
                {
                    AddAlternateTitle(entry, name.Groups["value"].Value);
                }
            }

            return entry;
        }

        public static List<uint> FindSteamAppIds(string script)
        {
            var ids = new List<uint>();
            void Add(string value)
            {
                if (uint.TryParse(value, out var id) && !ignoredAppIds.Contains(id) && !ids.Contains(id))
                {
                    ids.Add(id);
                }
            }

            foreach (var line in script.Split('\n'))
            {
                if (!appIdLineRegex.IsMatch(line))
                {
                    continue;
                }

                foreach (Match match in quotedNumberRegex.Matches(line))
                {
                    Add(match.Groups["id"].Value);
                }

                foreach (Match match in bareAppIdRegex.Matches(line))
                {
                    Add(match.Groups["id"].Value);
                }
            }

            foreach (Match match in appManifestRegex.Matches(script))
            {
                Add(match.Groups["id"].Value);
            }

            return ids;
        }

        /// <summary>
        /// Adds README titles that no installer script already covers, so games without a script are still listed.
        /// </summary>
        public static void AddReadmeTitles(List<VrModEntry> entries, string readme)
        {
            var known = new HashSet<string>(entries.SelectMany(e => new[] { e.Title }.Concat(e.AlternateTitles)).Select(DedupeKey));
            foreach (var title in ParseReadmeTitles(readme))
            {
                if (known.Add(DedupeKey(title)))
                {
                    entries.Add(new VrModEntry
                    {
                        Source = InstallerHubSource.SourceName,
                        Title = title,
                        Url = InstallerHubSource.RepoUrl
                    });
                }
            }
        }

        public static List<string> ParseReadmeTitles(string readme)
        {
            var titles = new List<string>();
            var inList = false;
            foreach (var rawLine in readme.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.IndexOf("currently listed titles", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    inList = true;
                    continue;
                }

                if (!inList)
                {
                    continue;
                }

                if (line.StartsWith("</details", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (line.StartsWith("* ") || line.StartsWith("- "))
                {
                    var title = line.Substring(2).Trim();
                    if (title.Length > 0)
                    {
                        titles.Add(title);
                    }
                }
            }

            return titles;
        }

        /// <summary>Titles found in the script, best first. Text containing PowerShell variables is skipped.</summary>
        private static List<string> FindTitles(string script)
        {
            var titles = new List<string>();
            foreach (var regex in titleRegexes)
            {
                foreach (Match match in regex.Matches(script))
                {
                    var title = installerSuffixRegex.Replace(match.Groups["title"].Value.Trim(), "");
                    if (title.Length > 0 && !title.Contains("$") && !titles.Contains(title, StringComparer.OrdinalIgnoreCase))
                    {
                        titles.Add(title);
                    }
                }
            }

            return titles;
        }

        /// <summary>"AlienIsolationVR" -> "Alien Isolation VR".</summary>
        public static string TitleFromFolder(string folder)
        {
            var spaced = Regex.Replace(folder, @"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|(?<=[A-Za-z])(?=\d)", " ", RegexOptions.None, SourceHttp.RegexTimeout);
            return spaced.Trim();
        }

        private static void AddAlternateTitle(VrModEntry entry, string title)
        {
            title = title.Trim();
            if (title.Length > 0
                && !string.Equals(title, entry.Title, StringComparison.OrdinalIgnoreCase)
                && !entry.AlternateTitles.Contains(title, StringComparer.OrdinalIgnoreCase))
            {
                entry.AlternateTitles.Add(title);
            }
        }

        private static string DedupeKey(string title)
        {
            return TitleNormalizer.Normalize(title, isModTitle: true).Key;
        }

        [DataContract]
        private class GitHubTreeResponse
        {
            [DataMember(Name = "sha")] public string Sha { get; set; }
            [DataMember(Name = "truncated")] public bool Truncated { get; set; }
            [DataMember(Name = "tree")] public List<GitHubTreeItem> Tree { get; set; }
        }

        [DataContract]
        private class GitHubTreeItem
        {
            [DataMember(Name = "path")] public string Path { get; set; }
            [DataMember(Name = "type")] public string Type { get; set; }
        }
    }
}
