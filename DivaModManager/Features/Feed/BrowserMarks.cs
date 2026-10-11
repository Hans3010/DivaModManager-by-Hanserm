using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using DivaModManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DivaModManager.Features.Feed
{
    /// <summary>
    /// GameBanana / DivaModArchive タブの「インストール済み」表示とお気に入りを管理する。
    /// お気に入りはカードのデータ（API の JSON）ごと favorites.json に保存するので、
    /// お気に入り一覧でも通常のカードと同じようにダウンロードできる。
    /// </summary>
    public static class BrowserMarks
    {
        private const string FAVORITES_FILE = "favorites.json";
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private class FavoriteEntry
        {
            public string Site { get; set; }
            public string Key { get; set; }
            public string Title { get; set; }
            public DateTime Added { get; set; }
            public string Data { get; set; }
        }

        private static List<FavoriteEntry> favorites;
        private static string FavoritesPath => Path.Combine(Global.assemblyLocation, FAVORITES_FILE);

        /// <summary>"https://gamebanana.com/mods/123/" と "gamebanana.com/mods/123" を同じものとして扱う</summary>
        public static string NormalizeLink(Uri uri)
        {
            if (uri == null) return null;
            var host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.")) host = host.Substring(4);
            return host + uri.AbsolutePath.TrimEnd('/').ToLowerInvariant();
        }

        /// <summary>インストール済み Mod の mod.json に保存されたページ URL の一覧</summary>
        public static HashSet<string> GetInstalledLinks()
        {
            var links = new HashSet<string>();
            var modsFolder = Global.ConfigJson?.CurrentConfig?.ModsFolder;
            if (string.IsNullOrEmpty(modsFolder) || !Directory.Exists(modsFolder)) return links;
            foreach (var dir in Directory.EnumerateDirectories(modsFolder))
            {
                var json = Path.Combine(dir, "mod.json");
                if (!File.Exists(json)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(json));
                    if (doc.RootElement.TryGetProperty("homepage", out var home) && Uri.TryCreate(home.GetString(), UriKind.Absolute, out var uri))
                        links.Add(NormalizeLink(uri));
                }
                catch (Exception) { }
            }
            return links;
        }

        private static List<FavoriteEntry> Favorites
        {
            get
            {
                if (favorites != null) return favorites;
                favorites = new List<FavoriteEntry>();
                try
                {
                    if (File.Exists(FavoritesPath))
                        favorites = JsonSerializer.Deserialize<List<FavoriteEntry>>(File.ReadAllText(FavoritesPath)) ?? new List<FavoriteEntry>();
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Couldn't read {FAVORITES_FILE}: {ex.Message}", LoggerType.Warning);
                }
                return favorites;
            }
        }

        private static void SaveFavorites()
        {
            try { File.WriteAllText(FavoritesPath, JsonSerializer.Serialize(Favorites, JsonOptions)); }
            catch (Exception ex) { Logger.WriteLine($"Couldn't save {FAVORITES_FILE}: {ex.Message}", LoggerType.Error); }
        }

        /// <summary>カードにインストール済み・お気に入りの印を付ける</summary>
        public static void Mark<T>(IEnumerable<T> items) where T : class
        {
            if (items == null) return;
            var installed = GetInstalledLinks();
            var favoriteKeys = new HashSet<string>(Favorites.Select(f => f.Key));
            foreach (var item in items)
            {
                switch (item)
                {
                    case GameBananaRecord gb:
                        var gbKey = NormalizeLink(gb.Link);
                        gb.IsInstalled = gbKey != null && installed.Contains(gbKey);
                        gb.IsFavorite = gbKey != null && favoriteKeys.Contains(gbKey);
                        break;
                    case DivaModArchivePost dma:
                        var dmaKey = NormalizeLink(dma.Link);
                        dma.IsInstalled = installed.Contains(dmaKey);
                        dma.IsFavorite = favoriteKeys.Contains(dmaKey);
                        break;
                }
            }
        }

        /// <summary>お気に入りの追加・解除を切り替える</summary>
        public static void ToggleFavorite(object item)
        {
            switch (item)
            {
                case GameBananaRecord gb:
                    gb.IsFavorite = Toggle("GB", NormalizeLink(gb.Link), gb.Title, gb);
                    break;
                case DivaModArchivePost dma:
                    dma.IsFavorite = Toggle("DMA", NormalizeLink(dma.Link), dma.Name, dma);
                    break;
            }
        }

        private static bool Toggle<T>(string site, string key, string title, T item)
        {
            if (key == null) return false;
            var existing = Favorites.FirstOrDefault(f => f.Key == key);
            if (existing != null)
            {
                Favorites.Remove(existing);
                SaveFavorites();
                return false;
            }
            string data;
            try { data = JsonSerializer.Serialize(item); }
            catch (Exception ex)
            {
                Logger.WriteLine($"Couldn't add \"{title}\" to favorites: {ex.Message}", LoggerType.Error);
                return false;
            }
            Favorites.Insert(0, new FavoriteEntry { Site = site, Key = key, Title = title, Added = DateTime.Now, Data = data });
            SaveFavorites();
            return true;
        }

        public static List<GameBananaRecord> GetFavoriteGameBanana() => Load<GameBananaRecord>("GB");

        public static List<DivaModArchivePost> GetFavoriteDivaModArchive() => Load<DivaModArchivePost>("DMA");

        private static List<T> Load<T>(string site) where T : class
        {
            var list = new List<T>();
            foreach (var f in Favorites.Where(x => x.Site == site))
            {
                try
                {
                    var item = JsonSerializer.Deserialize<T>(f.Data);
                    if (item != null) list.Add(item);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Couldn't load favorite \"{f.Title}\": {ex.Message}", LoggerType.Warning);
                }
            }
            Mark(list);
            return list;
        }
    }
}
