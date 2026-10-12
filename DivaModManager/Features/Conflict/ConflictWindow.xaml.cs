using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using DivaModManager.Features.Health;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DivaModManager.Features.Conflict
{
    /// <summary>
    /// 有効な Mod 同士の競合（ファイル・曲ID・モジュールID）と、Mod の問題（DLL・曲のファイル）を一覧表示する
    /// </summary>
    public partial class ConflictWindow : Window
    {
        private readonly Func<IReadOnlyList<(string Name, string Path)>> getEnabledMods;
        private readonly Func<IReadOnlyList<ModRequirements.InstalledMod>> getInstalledMods;

        public ConflictWindow(Func<IReadOnlyList<(string Name, string Path)>> getEnabledMods, Func<IReadOnlyList<ModRequirements.InstalledMod>> getInstalledMods = null)
        {
            InitializeComponent();
            this.getEnabledMods = getEnabledMods;
            this.getInstalledMods = getInstalledMods;
            // 列ヘッダーは共通の翻訳から除外されているので、ここで翻訳する（このウィンドウではコードが見出しを使わない）
            foreach (var grid in new[] { ConflictGrid, ProblemGrid })
                foreach (var column in grid.Columns)
                    if (column.Header is string header) column.Header = UiTranslator.T(header);
            Loaded += async (_, _) => await ScanAsync();
        }

        private async Task ScanAsync()
        {
            RescanButton.IsEnabled = false;
            SummaryText.Text = UiTranslator.T("Scanning enabled mods...");
            ConflictGrid.ItemsSource = null;
            ProblemGrid.ItemsSource = null;

            var mods = getEnabledMods();
            var installed = getInstalledMods?.Invoke();
            var gameDir = ModHealthScanner.GameDir;
            // GameBanana / DivaModArchive の「必要な Mod」を更新する（1 日 1 回）
            using (var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                try { await ModRequirements.RefreshAsync((installed ?? new List<ModRequirements.InstalledMod>()).Select(m => m.Homepage).Where(h => h != null), timeout.Token); }
                catch (Exception) { }
            }
            var (conflicts, problems) = await Task.Run(() => (ModConflictScanner.Scan(mods), ModHealthScanner.Scan(mods, gameDir, installed)));

            ConflictGrid.ItemsSource = conflicts;
            ProblemGrid.ItemsSource = problems.OrderBy(p => p.Severity).ThenBy(p => p.Mod).ToList();
            ConflictHeader.Text = $"{UiTranslator.T("Conflicts")} ({conflicts.Count})";
            ProblemHeader.Text = $"{UiTranslator.T("Problems")} ({problems.Count})";

            var parts = new List<string>();
            if (conflicts.Count > 0)
                parts.Add(UiTranslator.Format("{0} conflicts", conflicts.Count) + " (" + string.Join(", ", conflicts.GroupBy(c => c.TypeText).Select(g => $"{g.Count()} {g.Key}")) + ")");
            var errors = problems.Count(p => p.Severity == HealthSeverity.Error);
            if (errors > 0) parts.Add(UiTranslator.Format("{0} errors", errors));
            var others = problems.Count - errors;
            if (others > 0) parts.Add(UiTranslator.Format("{0} warnings", others));
            SummaryText.Text = parts.Count == 0
                ? UiTranslator.Format("No conflicts or problems found in your {0} enabled mods.", mods.Count)
                : UiTranslator.Format("Your {0} enabled mods: ", mods.Count) + string.Join(", ", parts);
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

        private void ProblemGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            InstallButton.IsEnabled = ProblemGrid.SelectedItem is HealthIssue { InstallUrl: not null };
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProblemGrid.SelectedItem is not HealthIssue { InstallUrl: not null } issue) return;
            InstallButton.IsEnabled = false;
            await RequirementInstaller.InstallAsync(issue.InstallUrl, issue.InstallName);
            InstallButton.IsEnabled = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
