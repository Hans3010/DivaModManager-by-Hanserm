using DivaModManager.Common.Helpers;
using DivaModManager.Features.Health;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DivaModManager.Features.Songs
{
    public enum SongStatus
    {
        InGame,
        Replaced,
        Disabled,
    }

    public class SongRow
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public string NameJp { get; init; }
        public string Mod { get; init; }
        public string Difficulties { get; init; }
        public SongStatus Status { get; init; }
        public string StatusText { get; init; }
    }

    /// <summary>
    /// List of the songs installed mods add (search by name or ID, which mod each song is from, and whether another mod with the same ID replaces it)
    /// </summary>
    public partial class SongListTab : UserControl
    {
        /// <summary>When a song is double-clicked, shows its mod in the Mods tab (handled by MainWindow)</summary>
        public static event Action<string> ShowModRequested;

        private List<SongRow> rows = new();
        private bool loading;

        public SongListTab()
        {
            InitializeComponent();
            // Column headers are excluded from the shared translation, so they're translated here
            foreach (var column in SongGrid.Columns)
                if (column.Header is string header) column.Header = UiTranslator.T(header);
        }

        /// <summary>Reloads the songs from the mod list (called when the tab is opened)</summary>
        public async Task RefreshAsync()
        {
            if (loading) return;
            loading = true;
            CountText.Text = UiTranslator.T("Loading songs...");
            // Order of the mod list = load priority (higher wins)
            var mods = (Global.ModList_All ?? new()).Select(m => (Name: m.name, Path: m.directory_path, Enabled: m.enabled)).ToList();
            rows = await Task.Run(() => Build(mods));

            var selected = ModFilterBox.SelectedItem as string;
            var allMods = UiTranslator.T("All mods");
            var modNames = new List<string> { allMods };
            modNames.AddRange(rows.Select(r => r.Mod).Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            ModFilterBox.ItemsSource = modNames;
            ModFilterBox.SelectedItem = selected != null && modNames.Contains(selected) ? selected : allMods;
            loading = false;
            ApplyFilter();
        }

        private static List<SongRow> Build(List<(string Name, string Path, bool Enabled)> mods)
        {
            var result = new List<SongRow>();
            // Among enabled mods, the first one that has the ID is used
            var winner = new Dictionary<int, string>();
            foreach (var (name, path, enabled) in mods)
            {
                foreach (var song in ModSongIndex.GetSongs(path))
                {
                    SongStatus status;
                    string text;
                    if (!enabled)
                    {
                        status = SongStatus.Disabled;
                        text = UiTranslator.T("Mod disabled");
                    }
                    else if (winner.TryGetValue(song.Id, out var other))
                    {
                        status = SongStatus.Replaced;
                        text = UiTranslator.Format("Replaced by {0} (same ID)", other);
                    }
                    else
                    {
                        winner[song.Id] = name;
                        status = SongStatus.InGame;
                        text = UiTranslator.T("In game");
                    }
                    result.Add(new SongRow { Id = song.Id, Name = song.Name, NameJp = song.NameJp == song.Name ? "" : song.NameJp, Mod = name, Difficulties = song.Difficulties, Status = status, StatusText = text });
                }
            }
            return result;
        }

        private void ApplyFilter()
        {
            if (loading || SongGrid == null || CountText == null) return;
            var query = SearchBox.Text.Trim();
            SearchHint.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            var mod = ModFilterBox.SelectedIndex > 0 ? ModFilterBox.SelectedItem as string : null;
            var onlyEnabled = OnlyEnabledBox.IsChecked == true;
            var onlyReplaced = ProblemsOnlyBox.IsChecked == true;

            var filtered = rows.Where(r =>
                (mod == null || r.Mod == mod)
                && (!onlyEnabled || r.Status != SongStatus.Disabled)
                && (!onlyReplaced || r.Status == SongStatus.Replaced || (r.Status == SongStatus.InGame && rows.Any(o => o.Id == r.Id && o.Status == SongStatus.Replaced)))
                && (query.Length == 0 || ModSongIndex.Matches(new ModSongIndex.Song(r.Id, r.Name, r.NameJp, ""), query) || r.Mod.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(r => r.Id).ThenBy(r => r.Status)
                .ToList();
            SongGrid.ItemsSource = filtered;
            var inGame = rows.Count(r => r.Status == SongStatus.InGame);
            CountText.Text = UiTranslator.Format("{0} songs shown · {1} songs in game from {2} mods", filtered.Count, inGame, rows.Where(r => r.Status == SongStatus.InGame).Select(r => r.Mod).Distinct().Count());
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
        private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();
        private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private void SongGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SongGrid.SelectedItem is SongRow row) ShowModRequested?.Invoke(row.Mod);
        }
    }
}
