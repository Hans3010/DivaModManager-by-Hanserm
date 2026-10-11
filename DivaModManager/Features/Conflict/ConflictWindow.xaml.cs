using DivaModManager.Features.Debug;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DivaModManager.Features.Conflict
{
    /// <summary>
    /// 有効な Mod 同士の競合（ファイル・曲ID・モジュールID）を一覧表示する
    /// </summary>
    public partial class ConflictWindow : Window
    {
        private readonly Func<IReadOnlyList<(string Name, string Path)>> getEnabledMods;

        public ConflictWindow(Func<IReadOnlyList<(string Name, string Path)>> getEnabledMods)
        {
            InitializeComponent();
            this.getEnabledMods = getEnabledMods;
            Loaded += async (_, _) => await ScanAsync();
        }

        private async Task ScanAsync()
        {
            RescanButton.IsEnabled = false;
            SummaryText.Text = "Scanning enabled mods...";
            ConflictGrid.ItemsSource = null;

            var mods = getEnabledMods();
            var conflicts = await Task.Run(() => ModConflictScanner.Scan(mods));

            ConflictGrid.ItemsSource = conflicts;
            SummaryText.Text = conflicts.Count == 0
                ? $"No conflicts found between your {mods.Count} enabled mods."
                : $"{conflicts.Count} conflicts found between your {mods.Count} enabled mods: "
                  + string.Join(", ", conflicts.GroupBy(c => c.TypeText).Select(g => $"{g.Count()} {g.Key}"));
            RescanButton.IsEnabled = true;
        }

        private async void RescanButton_Click(object sender, RoutedEventArgs e) => await ScanAsync();

        private void ConflictGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            FixButton.IsEnabled = ConflictGrid.SelectedItem is ModConflict c && ThumbnailConflictFixer.CanFix(c);
        }

        private async void FixButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConflictGrid.SelectedItem is not ModConflict conflict || !ThumbnailConflictFixer.CanFix(conflict)) return;

            var losers = conflict.Mods.Skip(1).ToList();
            var message = $"Song thumbnails conflict.\n\n{conflict.Winner} keeps the official thumbnail set.\n"
                + $"The thumbnails of {string.Join(", ", losers)} will be moved to their own sprite set, so both packs show their thumbnails in the song list.\n\n"
                + "The original files are kept as .bak in each mod's rom\\2d folder.\nContinue?";
            if (MessageBox.Show(this, message, "Fix thumbnails", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var mods = getEnabledMods();
            var results = new List<string>();
            foreach (var loser in losers)
            {
                var path = mods.FirstOrDefault(m => m.Name == loser).Path;
                try
                {
                    results.Add(ThumbnailConflictFixer.Fix(loser, path));
                    Logger.WriteLine(results.Last(), LoggerType.Info);
                }
                catch (Exception ex)
                {
                    results.Add($"{loser}: {ex.Message}");
                    Logger.WriteLine($"Fix thumbnails failed for {loser}: {ex.Message}", LoggerType.Error);
                }
            }
            MessageBox.Show(this, string.Join("\n\n", results), "Fix thumbnails", MessageBoxButton.OK, MessageBoxImage.Information);
            await ScanAsync();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
