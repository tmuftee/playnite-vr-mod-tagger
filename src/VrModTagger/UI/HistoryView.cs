using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using VrModTagger.Applying;

namespace VrModTagger.UI
{
    /// <summary>One line in a history list.</summary>
    public class HistoryRow
    {
        public HistoryRow(HistoryEntry entry)
        {
            Entry = entry;
        }

        public HistoryEntry Entry { get; }
        public DateTime? At => Entry.At;
        public string Date => Entry.At.HasValue ? Entry.At.Value.ToString("yyyy-MM-dd HH:mm") : "Before history";
        public string GameName => Entry.GameName;
        public string Change => Entry.Removed ? "Untagged" : "Tagged";
        public string Tags => string.Join(", ", Entry.Tags);
        public string Sources => string.Join(", ", Entry.Sources);
    }

    /// <summary>
    /// A list of history entries with a search box. Used for the sidebar view, the history window and the
    /// "newly tagged" pop-up. Built in code rather than XAML, like the review window.
    /// </summary>
    public class HistoryView : DockPanel
    {
        private readonly ICollectionView view;
        private readonly TextBox search;
        private readonly CheckBox showRemoved;
        private readonly TextBlock count;

        /// <param name="entries">The entries to list.</param>
        /// <param name="openGame">Called with a game's id when its row is double-clicked or Enter is pressed.</param>
        /// <param name="intro">Text above the list, or null.</param>
        /// <param name="compact">Hides the search box, the removals option and the date and change columns (for the pop-up).</param>
        public HistoryView(IEnumerable<HistoryEntry> entries, Action<Guid> openGame, string intro = null, bool compact = false)
        {
            Margin = new Thickness(12);
            LastChildFill = true;

            var rows = TagHistory.NewestFirst(entries).Select(e => new HistoryRow(e)).ToList();

            var top = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            if (intro != null)
            {
                top.Children.Add(new TextBlock { Text = intro, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
            }

            var filters = new DockPanel { LastChildFill = true };
            count = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Opacity = 0.7 };
            showRemoved = new CheckBox { Content = "Show untagged games", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            search = new TextBox { MinWidth = 200, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Search by game, tag or source" };
            DockPanel.SetDock(count, Dock.Right);
            DockPanel.SetDock(showRemoved, Dock.Right);
            filters.Children.Add(count);
            filters.Children.Add(showRemoved);
            filters.Children.Add(search);
            if (!compact)
            {
                top.Children.Add(filters);
            }

            SetDock(top, Dock.Top);
            Children.Add(top);

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                SelectionMode = DataGridSelectionMode.Single,
                ToolTip = "Double-click a game to show it in your library"
            };

            if (!compact)
            {
                grid.Columns.Add(new DataGridTextColumn { Header = "Date", Binding = new Binding(nameof(HistoryRow.Date)), SortMemberPath = nameof(HistoryRow.At) });
            }
            grid.Columns.Add(new DataGridTextColumn { Header = "Game", Binding = new Binding(nameof(HistoryRow.GameName)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            if (!compact)
            {
                grid.Columns.Add(new DataGridTextColumn { Header = "Change", Binding = new Binding(nameof(HistoryRow.Change)) });
            }
            grid.Columns.Add(new DataGridTextColumn { Header = compact ? "New tags" : "Tags", Binding = new Binding(nameof(HistoryRow.Tags)), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Sources", Binding = new Binding(nameof(HistoryRow.Sources)), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star) });

            view = CollectionViewSource.GetDefaultView(rows);
            view.Filter = Matches;
            grid.ItemsSource = view;

            void OpenSelected()
            {
                if (grid.SelectedItem is HistoryRow row)
                {
                    openGame(row.Entry.GameId);
                }
            }

            grid.MouseDoubleClick += (_, e) =>
            {
                if (e.OriginalSource is FrameworkElement element && element.DataContext is HistoryRow)
                {
                    OpenSelected();
                }
            };
            grid.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    OpenSelected();
                    e.Handled = true;
                }
            };

            search.TextChanged += (_, __) => Refresh();
            showRemoved.Checked += (_, __) => Refresh();
            showRemoved.Unchecked += (_, __) => Refresh();

            if (rows.Count == 0)
            {
                var empty = new TextBlock
                {
                    Text = "No games have been tagged yet. Run Extensions > VR Mod Tagger > Update VR mod tags.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.7
                };
                Children.Add(empty);
            }
            else
            {
                Children.Add(grid);
            }

            Refresh();
        }

        /// <summary>Panel at the bottom of a window, for its buttons.</summary>
        public void AddButtons(params Button[] buttons)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            foreach (var button in buttons)
            {
                button.MinWidth = 90;
                button.Margin = new Thickness(8, 0, 0, 0);
                panel.Children.Add(button);
            }

            SetDock(panel, Dock.Bottom);
            Children.Insert(1, panel);
        }

        private bool Matches(object item)
        {
            if (!(item is HistoryRow row))
            {
                return false;
            }

            if (row.Entry.Removed && showRemoved.IsChecked != true)
            {
                return false;
            }

            var text = search.Text?.Trim();
            return string.IsNullOrEmpty(text)
                || Contains(row.GameName, text)
                || Contains(row.Tags, text)
                || Contains(row.Sources, text)
                || Contains(row.Date, text);
        }

        private static bool Contains(string value, string text) => value != null && value.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0;

        private void Refresh()
        {
            view.Refresh();
            var shown = view.Cast<object>().Count();
            count.Text = shown == 1 ? "1 entry" : $"{shown} entries";
        }
    }
}
