using Playnite.SDK.Models;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace VrModTagger.Matching
{
    /// <summary>Builds matcher input from Playnite games. Kept apart from the matcher so tests don't need Playnite.</summary>
    public static class LibraryGameFactory
    {
        public static readonly Guid SteamLibraryPluginId = Guid.Parse("CB91DFC9-B977-43BF-8E70-55F46E410FAB");

        private static readonly Regex steamLinkRegex = new Regex(@"store\.steampowered\.com/(?:agecheck/)?app/(?<id>\d+)", RegexOptions.IgnoreCase);

        public static LibraryGame FromPlaynite(Game game)
        {
            var libraryGame = new LibraryGame
            {
                Id = game.Id,
                Name = game.Name,
                ReleaseYear = game.ReleaseDate?.Year
            };

            if (game.PluginId == SteamLibraryPluginId && uint.TryParse(game.GameId, out var appId))
            {
                libraryGame.SteamAppId = appId;
            }

            if (game.Links != null)
            {
                foreach (var link in game.Links.Where(l => l?.Url != null))
                {
                    var match = steamLinkRegex.Match(link.Url);
                    if (match.Success && uint.TryParse(match.Groups["id"].Value, out var linkedId)
                        && linkedId != libraryGame.SteamAppId && !libraryGame.LinkedSteamAppIds.Contains(linkedId))
                    {
                        libraryGame.LinkedSteamAppIds.Add(linkedId);
                    }
                }
            }

            return libraryGame;
        }
    }
}
