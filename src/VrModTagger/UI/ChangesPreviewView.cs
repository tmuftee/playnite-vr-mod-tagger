using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using VrModTagger.Applying;

namespace VrModTagger.UI
{
    /// <summary>
    /// Shown before changes are applied: the run summary, then every game that will change and how,
    /// with Apply and Cancel. Built in code like the other lists.
    /// </summary>
    public class ChangesPreviewView : DockPanel
    {
        /// <summary>True when the user pressed Apply.</summary>
        public bool Applied { get; private set; }

        public ChangesPreviewView(string summary, string changeCounts, IEnumerable<GameChange> changes, Window window, Action<Guid> openGame)
        {
            Margin = new Thickness(12);
            LastChildFill = true;

            var top = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            top.Children.Add(new TextBlock { Text = summary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
            top.Children.Add(new TextBlock { Text = "Changes:", FontWeight = FontWeights.Bold });
            top.Children.Add(new TextBlock { Text = changeCounts, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) });
            top.Children.Add(new TextBlock
            {
                Text = "Nothing changes until you press Apply. Double-click a game to show it in your library.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7
            });
            SetDock(top, Dock.Top);
            Children.Add(top);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var apply = new Button { Content = "Apply", MinWidth = 90, IsDefault = true };
            var cancel = new Button { Content = "Cancel", MinWidth = 90, Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
            apply.Click += (_, __) => { Applied = true; window.Close(); };
            cancel.Click += (_, __) => window.Close();
            buttons.Children.Add(apply);
            buttons.Children.Add(cancel);
            SetDock(buttons, Dock.Bottom);
            Children.Add(buttons);

            var grid = new DataGrid
            {
                ItemsSource = ChangePreview.Rows(changes),
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                SelectionMode = DataGridSelectionMode.Single
            };
            grid.Columns.Add(new DataGridTextColumn { Header = "Game", Binding = new Binding(nameof(ChangePreviewRow.GameName)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Adds tags", Binding = new Binding(nameof(ChangePreviewRow.AddedTags)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Removes tags", Binding = new Binding(nameof(ChangePreviewRow.RemovedTags)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "New links", Binding = new Binding(nameof(ChangePreviewRow.AddedLinks)), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Removed links", Binding = new Binding(nameof(ChangePreviewRow.RemovedLinks)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            grid.MouseDoubleClick += (_, e) =>
            {
                if (e.OriginalSource is FrameworkElement element && element.DataContext is ChangePreviewRow row)
                {
                    openGame(row.GameId);
                }
            };
            Children.Add(grid);
        }
    }
}
