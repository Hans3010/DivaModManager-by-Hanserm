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

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
