using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using VrModTagger.Matching;

namespace VrModTagger.UI
{
    /// <summary>One suggested match in the review list.</summary>
    public class FuzzyReviewRow
    {
        public GameMatch Match { get; set; }
        public bool Accept { get; set; }
        public string GameName => Match.Game.Name;
        public string EntryTitle => Match.Entry.Title;
        public string Source => Match.Entry.Source;
        public string Similarity => $"{Match.Score:P0}";
    }

    /// <summary>
    /// Lists suggested matches with a checkbox each. Built in code rather than XAML because it's a single grid.
    /// </summary>
    public class FuzzyReviewView : DockPanel
    {
        public List<FuzzyReviewRow> Rows { get; }

        /// <summary>True when the user pressed Save.</summary>
        public bool Saved { get; private set; }

        public FuzzyReviewView(IEnumerable<GameMatch> matches, Window window)
        {
            Rows = matches
                .OrderBy(m => m.Game.Name)
                .ThenByDescending(m => m.Score)
                .Select(m => new FuzzyReviewRow { Match = m, Accept = m.Approved })
                .ToList();

            Margin = new Thickness(12);
            LastChildFill = true;

            var intro = new TextBlock
            {
                Text = "These titles are similar but not identical. Tick the ones that are the same game; " +
                       "unticked suggestions won't be shown again.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            SetDock(intro, Dock.Top);
            Children.Add(intro);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var save = new Button { Content = "Save", MinWidth = 90, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true };
            save.Click += (_, __) => { Saved = true; window.Close(); };
            cancel.Click += (_, __) => window.Close();
            buttons.Children.Add(save);
            buttons.Children.Add(cancel);
            SetDock(buttons, Dock.Bottom);
            Children.Add(buttons);

            var grid = new DataGrid
            {
                ItemsSource = Rows,
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                SelectionMode = DataGridSelectionMode.Single
            };
            grid.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "Same game",
                Binding = new Binding(nameof(FuzzyReviewRow.Accept)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }
            });
            grid.Columns.Add(new DataGridTextColumn { Header = "Your game", Binding = new Binding(nameof(FuzzyReviewRow.GameName)), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Listed as", Binding = new Binding(nameof(FuzzyReviewRow.EntryTitle)), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Source", Binding = new Binding(nameof(FuzzyReviewRow.Source)), IsReadOnly = true });
            grid.Columns.Add(new DataGridTextColumn { Header = "Similarity", Binding = new Binding(nameof(FuzzyReviewRow.Similarity)), IsReadOnly = true });
            Children.Add(grid);
        }
    }
}
