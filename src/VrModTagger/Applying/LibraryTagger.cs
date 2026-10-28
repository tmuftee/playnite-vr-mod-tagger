using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace VrModTagger.Applying
{
    /// <summary>Reads game state from and writes planned changes to the Playnite database.</summary>
    public class LibraryTagger
    {
        private readonly IPlayniteAPI api;

        public LibraryTagger(IPlayniteAPI api)
        {
            this.api = api;
        }

        public List<GameState> ReadGames()
        {
            var tagNames = api.Database.Tags.ToDictionary(t => t.Id, t => t.Name);
            return api.Database.Games.Select(game => new GameState
            {
                Id = game.Id,
                Name = game.Name,
                TagNames = (game.TagIds ?? new List<Guid>())
                    .Select(id => tagNames.TryGetValue(id, out var name) ? name : null)
                    .Where(name => name != null)
                    .ToList(),
                Links = (game.Links ?? new ObservableCollection<Link>())
                    .Where(l => l != null)
                    .Select(l => new LinkInfo { Name = l.Name, Url = l.Url })
                    .ToList()
            }).ToList();
        }

        /// <summary>Applies the changes and returns those that were actually made (games deleted meanwhile are skipped).</summary>
        public List<GameChange> Apply(IEnumerable<GameChange> changes)
        {
            var applied = new List<GameChange>();
            using (api.Database.BufferedUpdate())
            {
                var updated = new List<Game>();
                foreach (var change in changes)
                {
                    var game = api.Database.Games.Get(change.GameId);
                    if (game == null)
                    {
                        continue;
                    }

                    var tagIds = game.TagIds?.ToList() ?? new List<Guid>();
                    foreach (var name in change.RemoveTags)
                    {
                        var tag = FindTag(name);
                        if (tag != null)
                        {
                            tagIds.Remove(tag.Id);
                        }
                    }

                    foreach (var name in change.AddTags)
                    {
                        var tag = FindTag(name) ?? api.Database.Tags.Add(name);
                        if (!tagIds.Contains(tag.Id))
                        {
                            tagIds.Add(tag.Id);
                        }
                    }

                    var links = game.Links?.ToList() ?? new List<Link>();
                    foreach (var remove in change.RemoveLinks)
                    {
                        links.RemoveAll(l => l != null && l.Name == remove.Name && string.Equals(l.Url, remove.Url, StringComparison.OrdinalIgnoreCase));
                    }

                    foreach (var add in change.AddLinks)
                    {
                        if (!links.Any(l => l != null && l.Name == add.Name && string.Equals(l.Url, add.Url, StringComparison.OrdinalIgnoreCase)))
                        {
                            links.Add(new Link(add.Name, add.Url));
                        }
                    }

                    game.TagIds = tagIds;
                    game.Links = new ObservableCollection<Link>(links);
                    updated.Add(game);
                    applied.Add(change);
                }

                if (updated.Count > 0)
                {
                    api.Database.Games.Update(updated);
                }
            }

            return applied;
        }

        private Tag FindTag(string name)
        {
            return api.Database.Tags.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
